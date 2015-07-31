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

from aste.workers.msworkers import MSBuildWorker
from aste.workers.mixins import TestRunnerMixin
from shutil import make_archive
from datetime import datetime
import os


class BoogieWorker(TestRunnerMixin, MSBuildWorker):
    """Implements the steps necessary to build Boogie.
    """

    def __init__(self, env):
        super(BoogieWorker, self).__init__(env, 'Boogie')

    def set_version_number(self):
        now = datetime.now()

        version = "%s.%s.%s%s.%s" % (self.cfg.VersionNumbers.Boogie.Major,
                                     self.cfg.VersionNumbers.Boogie.Minor,
                                     now.year - self.cfg.VersionNumbers.Boogie.YearZero,
                                     now.strftime('%m%d'),
                                     now.strftime('%H%M'))

        self.cd(self.cfg.Paths.Boogie + "\Build")
        cmd = "%s updateVersionFile.xml /p:CCNetLabel=%s" % (
             self.cfg.Apps.MSBuild, version)

        self.runSafely(cmd)

    def copySpecSharpToBoogie(self):
        self.cd(self.cfg.Paths.Boogie + "\Binaries")
        cmd = "%s SPECSHARPROOT=%s" % (self.cfg.Apps.nmake2010,
                                       self.cfg.Paths.SpecSharp)

        self.runSafely(cmd)

    def buildBoogie(self):
        self.cd(self.cfg.Paths.Boogie + "\Source")
        cmd = "%s Boogie.sln /Rebuild Checked" % self.cfg.Apps.devenv2010
        self._runDefaultBuildStep(cmd)

    def buildDafny(self):
        self.cd(self.cfg.Paths.Boogie + "\Source")
        cmd = "%s Dafny.sln /Rebuild Checked" % self.cfg.Apps.devenv2010
        self._runDefaultBuildStep(cmd)

    def testBoogie(self):
        failed = self.runTestFromAlltestsFile(
            self.cfg.Paths.Boogie + "\\Test\\alltests.txt", 'testBoogie',
            self.cfg.Flags.ShortTestsOnly)

        self.project_data['tests']['failed'] = failed

    def zip_binaries(self, filename):
        self.cd(self.cfg.Paths.Boogie + "\Binaries")
        cmd = "PrepareBoogieZip.bat"
        self.runSafely(cmd)
        # make_archive expects an archive name without a filename extension.
        archive_name = os.path.splitext(os.path.abspath(filename))[0]
        root_dir = os.path.abspath("export")
        make_archive(archive_name, 'zip', root_dir)
