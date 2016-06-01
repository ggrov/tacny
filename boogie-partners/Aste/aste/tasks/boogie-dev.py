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
Development tasks, not intended to be run from a productive system
"""

from aste.tasks.tasks import Task, AbstractBuildTask
from aste.workers.specsharpworkers import SpecSharpWorker
from aste.workers.boogieworkers import BoogieWorker
from aste.workers.sscboogieworkers import SscBoogieWorker

import aste.utils.errorhandling as errorhandling
from aste.tasks.boogie import BoogieTask, FullBuild, generate_report

class SpecSharpCheckinTests(Task):
    def run(self):
        sscWorker = SpecSharpWorker(self.env)
        sscWorker.buildSpecSharpCheckinTests()

class TestBoogie(Task):
    def run(self):
        boogieWorker = BoogieWorker(self.env)
        boogieWorker.testBoogie()

class TestSscBoogie(Task):
    def run(self):
        sscBoogieWorker = SscBoogieWorker(self.env)
        sscBoogieWorker.testSscBoogie()

class ReleaseBothBoogies(AbstractBuildTask):
    def __init__(self, env):
        super(ReleaseBothBoogies, self).__init__(env, None)

    def run(self):
        workers = [BoogieWorker(self.env), SscBoogieWorker(self.env)]

        for worker in workers:
            self.worker = worker
            self.upload_release(worker, revision=0)

class UploadBoogie(BoogieTask):
    @errorhandling.add_context("Building Boogie")
    def build(self):
        self.upload_release(self.worker)

        
class BuildOnly(FullBuild):
    def run(self):
        self.cfg.Flags.Tests = False
        super(BuildOnly, self).run()
