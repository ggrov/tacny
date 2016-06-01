# --------------------------------- LICENSE: ----------------------------------
# The file is part of Aste (pronounced "S-T"), an automatic build tool
# originally tailored towards Spec# and Boogie.
#
# Copyright (C) 2010  Malte Schwerhoff
#
# This program is free software; you can redistribute it and/or
# modify it under the terms of the GNU General Public License
# as published by the Free Software Foundation; either version 2
# of the License, or any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program; if not, write to the Free Software
# Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
# USA.
# --------------------------------- :LICENSE ----------------------------------


"""
 .. todo::
        Make it optional to not include the SVN checkout output (list of files)
        in the logs.
"""

import re
import shutil
import os
from difflib import unified_diff
from aste.workers.workers import BaseWorker
from aste.aste import AsteException
from aste.workers.mixins import SVNMixin, MercurialMixin
import aste.utils.errorhandling as errorhandling
from aste.utils.misc import ensure_directories_exist


class CheckoutWorker(MercurialMixin, SVNMixin, BaseWorker):
    """Checks out the sources of Spec#, Boogie and SscBoogie. The build data is
    stored using the corresponding method name, e.g. ``getSpecSharp``, as the
    key.

    If the configuration variable ``SVN.Update`` is false then a fresh checkout
    is done each time the worker runs. If ``SVN.Update`` is true then the local
    copy is updated (if existing and checked out otherwise).
    """

    DID = 'CheckoutWorker'

    def __init__(self, env):
        super(CheckoutWorker, self).__init__(env)
        if (not self.DID in self.env.data):
            self.env.data[self.DID] = {}

    @errorhandling.add_context("Checking out Spec# from CodePlex")
    def getSpecSharp(self):
        """Downloads the Spec# sources from ``SVN.SpecSharp`` to
        ``Paths.SpecSharp``.
        """
        result = self.hg_get_source(self.cfg.HG.SpecSharp, os.path.split(self.cfg.Paths.SpecSharp)[0], "SpecSharp", self.cfg.HG.Update)
        self.env.data[self.DID]['getSpecSharp'] = result
        self.env.data[self.DID]['getSscBoogie'] = result
        self.noteSummary('SpecSharp revision: %s' % result['last_changed_revision'],
                         prefix='# ')
        self.noteSummary('SscBoogie revision: %s' % result['last_changed_revision'],
                         prefix='# ')

    @errorhandling.add_context("Checking out Boogie from CodePlex")
    def getBoogie(self):
        """Downloads the Boogie sources from ``SVN.Boogie`` to
        ``Paths.Boogie``.
        """
        result = self.hg_get_source(self.cfg.HG.Boogie, self.cfg.Paths.Boogie, "Boogie", self.cfg.HG.Update)
        self.env.data[self.DID]['getBoogie'] = result
        self.noteSummary('Boogie revision: %s' % result['last_changed_revision'],
                         prefix='# ')


class CommitSummaryWorker(MercurialMixin, SVNMixin, BaseWorker):
    """
    ..todo:: Couple with the date format used by the logger.
    """

    ignorePattern = re.compile('''
            \[\d{4}-\d{2}-\d{2}\ \d{2}:\d{2}:\d{2}\]
                |
            ^\#.*
        ''', re.VERBOSE) #  | re.MULTILINE

    default_commit_message = "Committing summary"

    summary_current = ''

    summary_checkout_dir = ''

    summary_checkout_file = ''

    project = ''

    _VCS = "SVN"

    _url = None

    def __init__(self, env, project, VCS, url=None):
        """
        ``project`` must be a key of the configuration entry 'CommitSummary'.
        If requested, the current summary file will be copied to and committed
        from the directory 'CommitSummary.project.From'.
        """

        super(CommitSummaryWorker, self).__init__(env)

        self.project = project
        self._VCS = VCS
        self._url = url
        
        self.set_default_auth(self.cfg.CommitSummary[project].User,
                              self.cfg.CommitSummary[project].Password)

        self.summary_current = self.cfg.Logging.SummaryLog

        self.summary_checkout_dir = self.cfg.CommitSummary[project].Dir
        self.summary_checkout_file = os.path.join(
            self.summary_checkout_dir, os.path.basename(self.summary_current))

    def commit_summary_if_changed(self, message=None):
        committed = False

        if self.hasStatusChanged():
            if self.cfg.Flags.UploadSummary:
                committed = True
                self.commitCurrentSummary(message=message)
            else:
                self.note(("Summary (project=%s) has changed but won't be " +
                           "committed due to configuration flag " +
                           "'Flags.UploadSummary'.") % self.project)
        else:
            self.note(("Summary (project=%s) hasn't changed and won't be " +
                       "committed.") % self.project)

        return committed

    def hasStatusChanged(self):
        # (Re-)create that folder.
        ensure_directories_exist(self.summary_checkout_dir)

        differs = True

        # Diff current against repo summary, if the latter exists.
        if os.path.isfile(self.summary_checkout_file):
            differs = len(self.diff(self.summary_current,
                                    self.summary_checkout_file)) != 0

        return differs

    def diff(self, summaryA, summaryB):
        summaryA = self._readAndPrepare(summaryA)
        summaryB = self._readAndPrepare(summaryB)

        # Returns a generator producing the actual differences.
        # Attention: since diff is a generator it will only return its
        # content, i.e. the textual delta, only once!
        diff = unified_diff(summaryA, summaryB)
        diffstr = "".join(diff)

        return diffstr

    def _readAndPrepare(self, summary):
        with open(summary) as fh:
            lines = []

            for line in fh:
                lines.append(self.ignorePattern.sub('', line))

        return lines

    def commitCurrentSummary(self, message=None):
        self.commitSummary(self.summary_current, message=message)

    def commitSummary(self, summary, message=None):
        if message == None:
            message = self.default_commit_message
            
        if self._VCS == "SVN":
            self.svn_commit_summary(summary, message)
        elif self._VCS == "HG":
            self.hg_commit_summary(summary, message)
        else:
            raise NonBuildError("%s._VCS has unexpected value %s" % (self.__class__.__name__, self._VCS))

    def hg_commit_summary(self, summary, message):
        self.cd(self.cfg.Paths[self.project])
        shutil.copyfile(summary, self.summary_checkout_file)
        
        # Ensure that the summary we are about to commit has already been
        # added to the repo.        
        self.hg_ensure_version_controlled(self.summary_checkout_file)

        # Commit the current summary.
        self.hg_commit(self.summary_checkout_file, message)
        
        # Make sure we are in sync with the latest remote revision
        self._hg_run("shelve --all --force")
        self.hg_pull(self.cfg.Paths[self.project], rebase=True)
        
        # Push the summary
        self.hg_push(self._url)

    def svn_commit_summary(self, summary, message):
        shutil.copyfile(summary, self.summary_checkout_file)
        
        # Ensure that the summary we are about to commit has already been
        # added to the repo.
        self.svn_ensure_version_controlled(
            self.summary_checkout_file,
            user=self.cfg.CommitSummary[self.project].User,
            password=self.cfg.CommitSummary[self.project].Password)
            
        # Commit the current summary.
        self.svn_commit(self.summary_checkout_file, message,
                        user=self.cfg.CommitSummary[self.project].User,
                        password=self.cfg.CommitSummary[self.project].Password)