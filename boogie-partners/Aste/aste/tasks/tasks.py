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


from aste.aste import BuildError, NonBuildError
from aste.workers.svnworkers import CheckoutWorker, CommitSummaryWorker
from aste.workers.resultworkers import TimingsRecorder, ReleaseUploader
import aste.utils.misc

"""
Abstract tasks not specific to a certain build process and intended to be
specialised by sub-classing.
"""

class Task(object):
    _env = None

    def __init__(self, env):
        self._env = env

    @property
    def env(self):
        return self._env

    @property
    def cfg(self):
        return self.env.cfg
				
class AbstractBuildTask(Task):
    """Abstract class
    
    .. todo::
        "Complex" methods should be implemented by workers, such that tasks
        merely orchestrate worker methods.
    """

    def __init__(self, env, buildWorker):
        super(AbstractBuildTask, self).__init__(env)
        self.worker = buildWorker

    def build(self):
        """Abstract method"""
        pass

    def run(self, **kwargs):
        commit_summary = False

        # Errors are re-raised to the next layer (it should finally reach run.py
        # and trigger an error mail.
        # Note that
        #   'except BuildError: raise'
        # or
        #   'except BuildError as err: raise'
        # differ from 
        #   'except BuildError as err: raise err'
        # because the first two 'raise' will preserve the original stack trace
        # whereas the last 'raise' starts a new stack trace.
        
        try:
            self.build(**kwargs)
            commit_summary = True
        except BuildError:
            commit_summary = True
            raise
        finally:
            if commit_summary:
                self.commit_summary_if_changed(self.worker.project_data['build']['success'])

    def commit_summary_if_changed(self, success):
        message = '%s build ' % self.project

        if success:
            message += 'succeeded'
        else:
            message += 'failed'

        tests_failed = len(self.worker.project_data['tests']['failed'])
        if tests_failed != 0:
            message += ", %s test(s) failed" % tests_failed


        VCS = "SVN"
        url = None
        if self.project in self.cfg.HG:
            VCS = "HG"
            url = self.cfg.HG[self.project]

        committer = CommitSummaryWorker(self.env, self.project, VCS, url)
        if committer.commit_summary_if_changed(message=message):
            self.env.data['commits'].append(self.project)

    def upload_release(self, worker, revision=None):
        filename = self.project.lower() + "-nightly.zip"
        username = self.cfg.Nightlies[self.project].User
        password = aste.utils.misc.rot47(self.cfg.Nightlies[self.project].Password)
        projectname = self.cfg.Nightlies[self.project].Project

        if revision == None:
            # CheckoutWorker key is only present if this current run
            # includes checking-out the sources, which might not be the case.
            if "CheckoutWorker" in self.env.data:
                revision = self.env.data["CheckoutWorker"]['get' + self.project]['last_changed_revision']
            else:
                revision = 0
        
        release_name = None
        if 'ReleaseName' in self.cfg.Nightlies[self.project]:
            release_name = self.cfg.Nightlies[self.project].ReleaseName

        # ATTENTION: worker must implement zip_binaries() as expected!
        worker.zip_binaries(filename)
        ReleaseUploader(self.env).upload_release(projectname, revision,
                                                 username, password, filename,
                                                 release_name=release_name)

        worker.noteSummary('Released nightly of %s' % self.project, prefix='# ')

    @property
    def project(self):
        return self.worker.project