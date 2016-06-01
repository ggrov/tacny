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
Contains classes that are intended to be used as mixins
(by inheriting from them) in the sense that they provide workers with
additional services.
Classes in this module should not be intended to be instantiated directly.

In order to avoid problems with multiple inheritance, the mixins should not
override existing methods.
"""

import os
import re
import time
from aste.workers import workers
from aste.workers import msworkers
import aste.utils.misc
from aste.aste import NonBuildError

class TestRunnerMixin(msworkers.MSBuildWorker):
    """
    A mixin for :class:`aste.workers.msworkers.MSBuildWorker` that adds the
    functionality to execute tests from an 'alltests.txt'-style file and record
    their runtimes.

    .. todo::
        Isn't is sufficient to inherit from workers.BuildWorker instead of
        msworkers.MSBuildWorker?
    """

    def runTestFromAlltestsFile(self, filename, category, shortOnly=False):
        """
        Reads test cases from the 'alltests.txt'-style file ``filename``,
        executes them, records the runtimes in the build data under
        'timings/timings/<category>/<testcase>' where '<category>' is the given
        ``category`` and '<testcase>' is the name of the test case as taken from
        the alltests file. Finally, a summary of the results is logged.
        """

        path = os.path.dirname(filename)
        self.cd(path)

        # Matcher detecting a failed test case. The match group (*.?) captures
        # the name of the failing test case.
        matcher = [(
            ['(.*?) FAILED'], # non-greedy match
            [workers.accept], [str]
        )]

        # Number of executed tests
        tests = 0

        with open(filename, 'r') as fh:
            matches = []

            # Create a new global data entry for the timings in this category.
            # Existing timings (in memory, not on disc) in the category will be
            # overwritten.
            self.env.data['timings']['timings'][category] = {}
            timings = self.env.data['timings']['timings'][category]

            # Iterate over all tests in the given alltests-style file.
            for line in fh:
                row = re.split('\s+', line, 2)
                testcase = row[0]        # Name of the current test case
                testcat = row[1].lower() # Category of the current test (use, long, ...)

                # Decide whether or not to run the current test case.
                if testcat == 'use' or (testcat == 'long' and not shortOnly):
                    cmd = 'runtest.bat ' + testcase

                    # Execute the test and record the runtime.
                    startTime = time.time()
                    result = self.run(cmd)
                    elapsed = time.time() - startTime
                    elapsed = round(elapsed, 2)

                    # Store the elapsed runtime, search the output for failures and
                    # increase the number of executed test cases.
                    timings[testcase] = elapsed
                    matches += self.matchGroup(matcher, result['output'])
                    tests = tests + 1

        failed = len(matches)

        self.noteSummary("%s out of %s test(s) in %s failed"
                % (failed, tests, filename))

        if failed > 0:
            self.logSummary(str(matches))

        return matches


class SVNMixin(workers.BaseWorker):
    """
    A mixin for :class:`aste.workers.workers.BaseWorker` that adds the
    functionality to interact with SVN repositories.
    """

    __user = ""
    __password = ""

    def set_default_auth(self, user, password):
        """
        The ``password`` is expected to be rot47ed.
        """

        self.__user = user
        self.__password = password

    def _svn_run(self, arg, auth=True, user=None, password=None, abort=True):
        """
        .. todo:: Remove the abort-flag, we should play it safe and always
                  abort. If there are cases where a non-zero returncode
                  does not indicate an abort-worthy error, we should rather
                  pass an abort-detection function.
        """

        if user == None:
            user = self.__user

        if password == None:
            password = self.__password

        cmd = "%s %s --no-auth-cache --non-interactive" % (self.cfg.Apps.svn, arg)

        if auth:
            # logcmd does not contain the password, runcmd does.
            cmd += " --username %s --password %s" % (user, '%s')
            logcmd = cmd % '********'
            runcmd = cmd % aste.utils.misc.rot47(password)
        else:
            logcmd = cmd
            runcmd = cmd

        result = self.run(runcmd, logcmd=logcmd)

        if abort and result['returncode'] != 0:
            msg = "SVN action failed"
            self.abort(msg, command=logcmd, returncode=result['returncode'],
                       output=result['output'], exception_class=NonBuildError)

        return result
        
    def svn_get_source(self, svnUrl, destDir, update=False):
        """Checks out to or updates the local copy at ``destDir`` from the
        the SVN repository at ``svnUrl``, depending on the variable ``update``.

        Returns a dictionary with the ``returncode`` and the ``output`` of SVN, and
        the summary_current ``revision`` number.
        """
        if not update:
            if os.path.exists(destDir):
                # shutil.rmtree(destDir)     # Fails on Windows if a file inside
                #                            # destDir is read-only.
                cmd = "rmdir /S /Q %s" % destDir
                self.run(cmd, shell=True)

        if not os.path.exists(destDir):
            result = self.svn_checkout(svnUrl, destDir, auth=False)
        else:
            result = self.svn_update(destDir, auth=False)

        revisions = self.svn_get_revision_numbers(destDir)
        result.update(revisions)

        return result

    def svn_ensure_version_controlled(self, path, auth=True, user=None,
                                      password=None, abort=True):
        """
        Ensures that ``path`` is under version control.
        **Note**: This will fail if more than the last part of ``path`` are
        not yet under version control!
        """

        result = self._svn_run('status ' + path, auth=auth, user=user,
                               password=password, abort=abort)

        if result['output'].startswith('?'):
            # SVN does not know about the path, hence we have to add it
            # to the repository.
            result = self._svn_run('add ' + path, auth=auth, user=user,
                                   password=password, abort=abort)

    def svn_update(self, path, auth=True, user=None, password=None, abort=True):
        arg = 'update %s' % path

        return self._svn_run(arg, auth=auth, user=user, password=password,
                             abort=abort)

    def svn_revert(self, path, abort=True):
        arg = 'revert ' + path

        return self._svn_run(arg, auth=False, abort=abort)

    def svn_get_revision_numbers(self, path, abort=True):
        """
        Returns the 'revision' and the 'last changed revision' number by
        matching the the output of ``svn info``.
        """

        result = self._svn_run('info ' + path, auth=False, abort=abort)

        revision = re.search('^Revision: (\d+)',
                             result['output'],
                             re.MULTILINE).group(1)
        revision = revision

        last_changed_revision = re.search('^Last Changed Rev: (\d+)',
                                          result['output'],
                                          re.MULTILINE).group(1)
        last_changed_revision = last_changed_revision

        return {
            'revision': revision,
            'last_changed_revision': last_changed_revision
        }

    def svn_checkout(self, url, localdir, auth=True, user=None, password=None,
                     abort=True):

        arg = 'checkout %s %s' % (url, localdir)

        return self._svn_run(arg, auth=auth, user=user, password=password,
                             abort=abort)

    def svn_commit(self, path, msg, auth=True, user=None, password=None,
                   abort=True):
        """
        Commits the ``path``, which must already be under version control.
        The ``password`` is expected to be rot47ed.
        """

        arg = 'commit -m "%s" %s' % (msg, path)

        return self._svn_run(arg, auth=auth, user=user, password=password,
                             abort=abort)


class MercurialMixin(workers.BaseWorker):
    """
    A mixin for :class:`aste.workers.workers.BaseWorker` that adds the
    functionality to interact with Mercurial repositories.
    """

    __user = None     # String if set
    __password = None # String if set

    def set_default_auth(self, user, password):
        """
        The ``password`` is expected to be rot47ed.
        """
		
        self.__user = user
        self.__password = aste.utils.misc.rot47(password)

    def _insert_credentials(self, url, username, password):
        return url.replace("https://", "%s%s:%s@" % ("https://", username, password))

    def _hg_run(self, arg, logarg=None, abort=True):
        """
        .. todo:: Remove the abort-flag, we should play it safe and always
                  abort. If there are cases where a non-zero returncode
                  does not indicate an abort-worthy error, we should rather
                  pass an abort-detection function.
                  
        .. todo:: HG.CLIArgs might contain information that should not
                  appear in the logfile.
        """

        cliArgs = self.cfg.HG['CLIArgs'] if 'CLIArgs' in self.cfg.HG else ''
        
        cmd = "%s %s %s" % (self.cfg.Apps.hg, cliArgs, arg)
        if logarg == None:
            logarg = arg
        logcmd = "%s %s %s" % (self.cfg.Apps.hg, cliArgs, logarg)

        result = self.run(cmd, logcmd=logcmd)

        if abort and result['returncode'] != 0:
            msg = "HG action failed"
            self.abort(msg, command=logcmd, returncode=result['returncode'],
                       output=result['output'], exception_class=NonBuildError)

        return result
        
    def hg_get_source(self, url, destDir, project, update=False):        
        """Checks out to or updates the local copy at ``destDir`` from the
        the Mercurial repository at ``url``, depending on the variable ``update``.

        Returns a dictionary with the ``returncode`` and the ``output`` of HG,
        and the current ``revision`` number.
        """
        if not update:
            if os.path.exists(destDir):
                # shutil.rmtree(destDir)     # Fails on Windows if a file inside
                #                            # destDir is read-only.
                cmd = "rmdir /S /Q %s" % destDir
                self.run(cmd, shell=True)
                
                if (os.path.exists(destDir)):
                    raise NonBuildError("'%s' could not be deleted.")

        if not os.path.exists(destDir):
            result = self.hg_checkout(url, destDir)
        else:
            self.cd(destDir)
            result = self.hg_pull()
            result = self.hg_update()

        self.cd(destDir)

        revisions = self.hg_get_revision_numbers()
        result.update(revisions)

        return result

    def hg_ensure_version_controlled(self, path, abort=True):
        """
        Ensures that ``path`` is under version control.
        **Note**: This will fail if more than the last part of ``path`` are
        not yet under version control!
        """

        result = self._hg_run('status ' + path, abort=abort)

        if result['output'].startswith('?'):
            # HG does not know about the path, hence we have to add it
            # to the repository.
            result = self._hg_run('add ' + path, abort=abort)

    def hg_update(self, abort=True):

        return self._hg_run("update", abort=abort)

    def hg_revert(self, path, abort=True):

        return self._hg_run('revert ' + path, abort=abort)

    def hg_get_revision_numbers(self, abort=True):
        """
        Returns the 'revision' and the 'last changed revision' number by
        matching the the output of ``hg id -i``.
        """

        result = self._hg_run('id -i', abort=abort)

        revision = result['output'].strip()

        return {
            'revision': revision,
            'last_changed_revision': revision
        }

    def hg_checkout(self, url, localdir, abort=True):

        arg = 'clone %s %s' % (self._insert_credentials(url, self.__user, self.__password), localdir)
        logarg = 'clone %s %s' % (self._insert_credentials(url, self.__user, "********"), localdir)

        return self._hg_run(arg, logarg=logarg, abort=abort)

    def hg_commit(self, path, msg, abort=True):
        """
        Commits the ``path``, which must already be under version control.
        The ``password`` is expected to be rot47ed.
        """

        arg = 'commit --message "%s" --user "CodeplexBot" %s' % (msg, path)

        return self._hg_run(arg, abort=abort)

    def hg_push(self, url, abort=True):
        """
        Push to the repository under ``url``.
        The ``password`` is expected to be rot47ed.
        """

        arg = 'push ' + self._insert_credentials(url, self.__user, self.__password)
        logarg = 'push ' + self._insert_credentials(url, self.__user, "********")
        
        return self._hg_run(arg, logarg=logarg, abort=abort)
        
    def hg_pull(self, abort=True, rebase=False):
        """
        .. todo::
            The following python code can be used e.g. when starting Aste to
            ensure that the rebase extension is enabled:
            ``re.search('enabled extensions:\s+rebase(\s+.*\s+)+disabled extensions:', output) != None``,
            where ``output`` is the output of ``hg help extensions``.
            
            Something more direct, e.g. ``hg is_extension_enabled rebase`` would
            obviously be better.
        """

        arg = 'pull'
        if rebase: arg += ' --rebase'

        return self._hg_run(arg, abort=abort)
