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
Tasks specific to the build process of Chalice.
"""

from aste.tasks.tasks import Task, AbstractBuildTask
from aste.workers.chaliceworkers import ChaliceWorker
from aste.reporting.reporting import generate_summary_file_urls_from_config
from aste.reporting.chalice import generate_additional_information
import aste.utils.errorhandling as errorhandling
import aste.utils.misc

def generate_report(indent, env):
    text = ''

    text += generate_additional_information(indent, env)
    text += generate_summary_file_urls_from_config(indent, env)

    return text

class ChaliceTask(AbstractBuildTask):
    def __init__(self, env):
        super(ChaliceTask, self).__init__(env, ChaliceWorker(env))

    def runBuild(self):
        self.worker.buildChalice()

    def runTests(self):
        self.worker.testChalice()

    @errorhandling.add_context("Building Chalice")
    def build(self):
        self.worker.check_z3_version('4.1')
        self.worker.checkoutChalice()

        self.worker.project_data['build']['started'] = True
        self.runBuild()
        self.worker.project_data['build']['success'] = True

        if self.cfg.Flags.Tests:
            self.runTests()

        if self.cfg.Flags.UploadTheBuild:
            self.upload_release(self.worker)
                
class FullBuild(Task):
    """
    This is the major build task for building Chalice.
    """
    def run(self):
        self.env.data['taskname'] = 'Chalice'
        ChaliceTask(self.env).run()
