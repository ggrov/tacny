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
Tasks specific to the build process of Boogie and Spec#.
"""

from aste.tasks.tasks import Task, AbstractBuildTask
from aste.workers.svnworkers import CheckoutWorker
from aste.workers.specsharpworkers import SpecSharpWorker
from aste.workers.boogieworkers import BoogieWorker
from aste.workers.sscboogieworkers import SscBoogieWorker
from aste.workers.resultworkers import TimingsRecorder
from aste.workers.miscworkers import TimingsCSVExporter
from aste.reporting.boogie import generate_additional_information
from aste.reporting.reporting import generate_summary_file_urls_from_config
import aste.utils.errorhandling as errorhandling

def generate_report(indent, env):
    text = ''

    text += generate_additional_information(indent, env)
    text += generate_summary_file_urls_from_config(indent, env)

    return text    

class CheckoutTask(Task):
    def run(self):
        checkoutWorker = CheckoutWorker(self.env)

        checkoutWorker.getSpecSharp()
        checkoutWorker.getBoogie()

class SpecSharpTask(AbstractBuildTask):
    def __init__(self, env):
        super(SpecSharpTask, self).__init__(env, SpecSharpWorker(env))

    @errorhandling.add_context("Building Spec#")
    def build(self):
        self.worker.project_data['build']['started'] = True

        self.worker.registerSpecSharpLKG()

        # self.worker.buildParserHelper()

        # self.worker.copyParserHelperToSpecSharp()

        self.worker.buildSpecSharp()

        if self.cfg.Flags.Tests and self.cfg.Flags.CheckinTests:
            self.worker.buildSpecSharpCheckinTests()

        self.worker.registerSpecSharpCompiler()

        self.worker.project_data['build']['success'] = True


class BoogieTask(AbstractBuildTask):
    def __init__(self, env):
        super(BoogieTask, self).__init__(env, BoogieWorker(env))

    def runBuild(self):
        """
        .. todo:: Move buildDafny() to a dedicated worker and task.
        """

        # self.worker.copySpecSharpToBoogie()

        self.worker.set_version_number()

        self.worker.buildBoogie()

        if self.cfg.Flags.Dafny:
            self.worker.buildDafny()

    def runTests(self):
        self.worker.testBoogie()

    @errorhandling.add_context("Building Boogie")
    def build(self):
        self.worker.project_data['build']['started'] = True
        self.runBuild()
        self.worker.project_data['build']['success'] = True

        if self.cfg.Flags.Tests:
            self.runTests()

            if self.cfg.Flags.UploadTheBuild:
                self.upload_release(self.worker)


class SscBoogieTask(AbstractBuildTask):
    def __init__(self, env):
        super(SscBoogieTask, self).__init__(env, SscBoogieWorker(env))

    @errorhandling.add_context("Building SscBoogie")
    def build(self):
        self.worker.project_data['build']['started'] = True
        self.worker.buildSscBoogie()
        self.worker.registerSscBoogie()
        self.worker.project_data['build']['success'] = True

        if self.cfg.Flags.Tests:
            self.worker.testSscBoogie()

            if self.cfg.Flags.UploadTheBuild:
                self.upload_release(self.worker)

class FullBuild(Task):
    """
    This is the major build task for building Boogie.
    """
    def run(self):
        CheckoutTask(self.env).run()
        SpecSharpTask(self.env).run()
        BoogieTask(self.env).run()

        if self.cfg.Flags.SscBoogie:
            SscBoogieTask(self.env).run()


# TODO: 2011-08-17 Malte:
#   RecordTimings and ExportTimingsCSV should be generic. Check, and if so,
#   move to tasks/utils.py (or create a tasks/timings.py).
#   Possible problem: Are the files used to store the timings hard-coded?
class RecordTimings(Task):
    @errorhandling.add_context("Recording test timings")
    def run(self):
        if len(self.env.data['timings']['timings']) > 0:
            worker = TimingsRecorder(self.env)
            worker.add(self.env.data['timings'])

class ExportTimingsCSV(Task):
    @errorhandling.add_context("Exporting test timings to a CSV file")
    def run(self):
        worker = TimingsCSVExporter(self.env)
        worker.export(self.cfg.Timings.CSV)
