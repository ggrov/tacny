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


class SpecSharpWorker(MSBuildWorker):
    """
    Implements the steps necessary to build Spec#.
    """

    def __init__(self, env):
        super(SpecSharpWorker, self).__init__(env, 'SpecSharp')

    def registerSpecSharpLKG(self):
        self.cd(self.cfg.Paths.SpecSharp + "\Microsoft.SpecSharp\LastKnownGood10")

        cmd = "Register.cmd Clean %s " % self.cfg.Apps.regasm
        self.runSafely(cmd)

        cmd = "Register.cmd RegisterLKG %s " % self.cfg.Apps.regasm
        self.runSafely(cmd)

    def buildParserHelper(self):
        self.cd(self.cfg.Paths.Boogie + "\Source")
        cmd = "%s Boogie.sln /Project \"ParserHelper\" /Rebuild" % self.cfg.Apps.devenv2010
        self._runDefaultBuildStep(cmd)

    def copyParserHelperToSpecSharp(self):
        self.cd(self.cfg.Paths.SpecSharp + "\Binaries")
        cmd = "%s BOOGIEROOT=%s" % (self.cfg.Apps.nmake2010, self.cfg.Paths.Boogie)
        self.runSafely(cmd)

    def buildSpecSharp(self):
        self.cd(self.cfg.Paths.SpecSharp)
        cmd = "%s SpecSharp.sln /Rebuild Debug" % self.cfg.Apps.devenv2010
        self._runDefaultBuildStep(cmd)

    def buildSpecSharpCheckinTests(self):
        self.cd(self.cfg.Paths.SpecSharp)

        cmd = "%s SpecSharp.sln /Project \"Checkin Tests\" /Build" % self.cfg.Apps.devenv2010

        self._runDefaultBuildStep(cmd)

    def registerSpecSharpCompiler(self):
        self.cd(self.cfg.Paths.SpecSharp + "\Microsoft.SpecSharp\Registration")
        cmd = "cmd.exe /c \"call \"%s\VC\\vcvarsall.bat\" x86 & RegisterCurrent.cmd\"" % self.cfg.Apps.VisualStudio2010
        self.runSafely(cmd)
