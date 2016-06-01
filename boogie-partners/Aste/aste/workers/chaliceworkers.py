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

import os
import re
from shutil import make_archive
from aste.aste import BuildError, NonBuildError
from aste.workers.workers import BaseWorker, BuildWorker, accept, raiseNonBuildError
from aste.workers.mixins import MercurialMixin
import aste.workers.svnworkers
import aste.utils.errorhandling as errorhandling

class CheckoutWorker(MercurialMixin, BaseWorker):
    """Checks out the sources of Chalice. The build data is stored in the
    environment using the corresponding method name as the key, e.g.
    in ``env.data[CheckoutWorker.DID]['getChalice']``.
    """

    # That DID is hard-coded into reporting/reporting.py in order to generate
    # parts of the report, hence we use the same here.
    DID = aste.workers.svnworkers.CheckoutWorker.DID

    def __init__(self, env):
        super(CheckoutWorker, self).__init__(env)
        if (not self.DID in self.env.data):
            self.env.data[self.DID] = {}

    @errorhandling.add_context("Checking out Chalice from CodePlex")
    def getChalice(self):
        """Downloads the Chalice sources from ``HG.Chalice`` to
        ``Paths.Chalice``.
        """
        
        result = self.hg_get_source(self.cfg.HG.Chalice, self.cfg.Paths.Chalice, "Chalice", self.cfg.HG.Update)
        
        self.env.data[self.DID]['getChalice'] = result

        self.noteSummary('Chalice revision: %s' % result['last_changed_revision'],
                         prefix='# ')


def _raiseBuildError(match):
    raise BuildError("Aborted due to error: " + match)

class ChaliceMatchingWorker(BuildWorker):
    def __init__(self, env, project_name):
        super(ChaliceMatchingWorker, self).__init__(env, project_name)
        
    matchers = {
        'build': [
            (
                ['(\[warn\] .+:\d+: .*)'],
                # ['(?m)^(\[warn\] .+:\d+: .*)$'],
                [accept],
                [str]
            ), (
                ['(\[error\] .+:\d+: .*)'],
                # ['(?m)^(\[error\] .+:\d+: .*)$'],
                [_raiseBuildError], # Python 2.7: Can't raise an error inside a lambda expression, hence this method call
                [str]
            )
        ],
        'envfatals': [
            (
                ['(java.io.IOException: .*)'],
                [raiseNonBuildError], [lambda match: match[0]]
            ), (
                ['The process cannot access the file because it is being used by another process'],
                [raiseNonBuildError], [lambda match: match[0]]
            )
        ]
    }
    
    def _matchDefaults(self, output):
        """Matches the ``output`` with the matcher groups
        *envfatals* and *build*, and returns the matches in a single list.
        """
        
        # output = _mock_compile_output_warnings
            # Mock output is defined at the end of this file
        
        matches = self.matchNamedGroup('envfatals', output)
        matches += self.matchNamedGroup('build', output)

        return matches


class ChaliceWorker(ChaliceMatchingWorker):
    """Implements the steps necessary to build and test Chalice.
    """

    def __init__(self, env):
        super(ChaliceWorker, self).__init__(env, 'Chalice')

    # def set_version_number(self):
        # """
        # .. todo::
            # Give Chalice a build version/timestamp that makes it possible to
            # identify and order Chalice builds.
        # """
        
    def checkoutChalice(self):
        checkoutWorker = CheckoutWorker(self.env)
        checkoutWorker.getChalice()

    def __runSbtBasedTool(self, sbtCmd):
        cmdSetIvyHome = "set JAVA_OPTS=-Dsbt.ivy.home=" + self.cfg.Paths.Sbt.IvyHome
        cmd = 'cmd /c "(%s) && (%s)"' % (cmdSetIvyHome, sbtCmd)
        
        # Execute command and match against error/success patterns. These patterns should
        # include Sbt errors and failures and Java exceptions.
        return self._runDefaultBuildStep(cmd)
        
    def buildChalice(self):
        self.cd(self.cfg.Paths.Chalice + "\\Chalice")
        
        # cmdSetIvyHome = "set JAVA_OPTS=-Dsbt.ivy.home=" + self.cfg.Paths.Sbt.IvyHome
        
        cmd = "sbt.bat clean compile"
        
        # cmd = 'cmd /c "(%s) && (%s)"' % (cmdSetIvyHome, cmdSbt)
        # cmd = 'cmd /c "%s"' % (cmdSbt)
        # cmd = "%s" % (cmdSbt)
        
        # cmd = 'cmd.exe /C rem'
            # Noop cmd, for example for the case that 
            # ChaliceMatchingWorker._matchDefaults overwrites the received
            # output with mock output.
        
        # self._runDefaultBuildStep(cmd)
        self.__runSbtBasedTool(cmd)

    def testChalice(self):
        # Matcher detecting a failed test case. The match group (*.?) captures
        # the name of the failing test case.
        failMatcher = [(['(?m)^FAIL: (.*?)$', '(?m)^ERROR: (.*?)$'], [accept], [str])]
        successMatcher = [(['(?m)^OK: (.*?)$'], [accept], [str])]
        
        summaryMatcher = [(
            ['(?:SUMMARY: completed (\d+) tests successfully.)|(?:SUMMARY: \d+ of (\d+) tests failed.)'],
            [accept],
            [lambda match: int(next((e for e in match if e.isdigit()), None))]
        )]
        
        self.cd(self.cfg.Paths.Chalice + "\\Chalice\\tests")
        
        cmd = "runalltests.bat /boogie:%s /boogieOpt:z3exe:%s" % (self.cfg.Apps.Boogie, self.cfg.Apps.Z3)
        
        # result = {'output': mock_test_output_fails_oks}
            # Mock output is defined at the end of this file

        result = self.run(cmd)
        
        failMatches = self.matchGroup(failMatcher, result['output'])
        successMatches = self.matchGroup(successMatcher, result['output'])
        summaryMatches = self.matchGroup(summaryMatcher, result['output'])
        
        tests = summaryMatches[0]
        
        self.noteSummary("%s out of %s test(s) failed" % (len(failMatches), tests))

        if len(failMatches) > 0:
            self.logSummary(str(failMatches))

        self.project_data['tests']['failed'] = failMatches
        
    def check_z3_version(self, required_version_str):
        """
        .. todo:: Accepts only one Z3 version, use range min-max instead.
        """

        cmd = "%s /version" % self.cfg.Apps.Z3
        result = self.runSafely(cmd)
        
        match = re.match('Z3 version (\d+\.?\d*)', result['output'])
        found_version_str = match.group(1)
        
        if found_version_str != required_version_str:
            msg = "Expected Z3 %s but found: %s" % (required_version_str, found_version_str)
            self.abort(msg, command=cmd, returncode=result['returncode'],
                       output=result['output'], exception_class=NonBuildError)

    def zip_binaries(self, filename):
        self.cd(self.cfg.Paths.Chalice + "\\Chalice\\scripts\\create_release")
        cmd = "create_release.bat"
        # self.runSafely(cmd)
        self.__runSbtBasedTool(cmd)
        # make_archive expects an archive name without a filename extension.
        archive_name = os.path.splitext(os.path.abspath(filename))[0]
        root_dir = os.path.abspath("release")
        make_archive(archive_name, 'zip', root_dir)
        
        
# _mock_compile_output_warnings = """
# [info] Set current project to default-c3764d (in build file:/C:/Temp/aste/Boogie/Chalice/)
# [success] Total time: 1 s, completed 17.08.2011 15:37:00
# [info] Updating {file:/C:/Temp/aste/Boogie/Chalice/}default-c3764d...
# [info] Done updating.
# [info] Compiling 11 Scala sources to C:\Temp\aste\Boogie\Chalice\target\scala-2.8.1.final\classes...
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:77: case class `class SeqClass' has case class ancestor `class Class'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] sealed case class SeqClass(parameter: Class) extends Class("seq", List(parameter), "default", Nil) {
# [warn]                   ^
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:111: case class `class TokenClass' has case class ancestor `class Class'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] case class TokenClass(c: Type, m: String) extends Class("token", Nil, "default", List(
# [warn]            ^
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:121: case class `class ChannelClass' has case class ancestor `class Class'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] case class ChannelClass(ch: Channel) extends Class(ch.id, Nil, "default", Nil) {
# [warn]            ^
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:141: case class `class TokenType' has case class ancestor `class Type'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] sealed case class TokenType(C: Type, m: String) extends Type("token", Nil) {  // denotes the use of a type
# [warn]                   ^
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:168: case class `class SpecialField' has case class ancestor `class Field'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] case class SpecialField(name: String, tp: Type, hidden: Boolean) extends Field(name, tp, false) {  // direct assignments are not allowed to a SpecialField
# [warn]            ^
# [warn] C:\Temp\aste\Boogie\Chalice\src\main\scala\Ast.scala:212: case class `class SpecialVariable' has case class ancestor `class Variable'.  This has been deprecated for unduly complicating both usage and implementation.  You should instead use extractors for pattern matching on non-leaf nodes.
# [warn] case class SpecialVariable(name: String, typ: Type) extends Variable(name, typ, false, false) {
# [warn]            ^
# [warn] 6 warnings found
# [success] Total time: 49 s, completed 17.08.2011 15:37:50
# """

# mock_test_output_fails_oks = """
# Running tests in examples ...
# ------------------------------------------------------
# FAIL: AssociationList.chalice
# FAIL: BackgroundComputation.chalice
# FAIL: cell.chalice
# FAIL: CopyLessMessagePassing-with-ack.chalice
# FAIL: CopyLessMessagePassing-with-ack2.chalice
# FAIL: CopyLessMessagePassing.chalice
# FAIL: dining-philosophers.chalice
# FAIL: FictionallyDisjointCells.chalice
# FAIL: ForkJoin.chalice
# FAIL: HandOverHand.chalice
# FAIL: iterator.chalice
# FAIL: iterator2.chalice
# FAIL: linkedlist.chalice
# FAIL: OwickiGries.chalice
# FAIL: PetersonsAlgorithm.chalice
# FAIL: ProdConsChannel.chalice
# FAIL: producer-consumer.chalice
# FAIL: RockBand.chalice
# FAIL: Sieve.chalice
# FAIL: Solver.chalice
# FAIL: swap.chalice
# FAIL: TreeOfWorker.chalice
# FAIL: UnboundedThreads.chalice
# ------------------------------------------------------
# Running tests in permission-model ...
# ------------------------------------------------------
# FAIL: basic.chalice
# FAIL: channels.chalice
# FAIL: locks.chalice
# FAIL: peculiar.chalice
# OK: permarith_parser.chalice
# FAIL: permission_arithmetic.chalice
# FAIL: predicates.chalice
# FAIL: predicate_error1.chalice
# FAIL: predicate_error2.chalice
# FAIL: predicate_error3.chalice
# FAIL: predicate_error4.chalice
# FAIL: scaling.chalice
# FAIL: sequences.chalice
# ------------------------------------------------------
# Running tests in general-tests ...
# ------------------------------------------------------
# FAIL: cell-defaults.chalice
# FAIL: counter.chalice
# FAIL: ImplicitLocals.chalice
# FAIL: LoopLockChange.chalice
# OK: prog0.chalice
# FAIL: prog1.chalice
# FAIL: prog2.chalice
# FAIL: prog3.chalice
# FAIL: prog4.chalice
# FAIL: quantifiers.chalice
# FAIL: RockBand-automagic.chalice
# FAIL: SmokeTestTest.chalice
# OK: VariationsOfProdConsChannel.chalice
# ------------------------------------------------------
# Running tests in regressions ...
# ------------------------------------------------------
# OK: workitem-10147.chalice
# FAIL: workitem-10190.chalice
# FAIL: workitem-10192.chalice
# FAIL: workitem-10194.chalice
# FAIL: workitem-10195.chalice
# FAIL: workitem-10196.chalice
# FAIL: workitem-10197.chalice
# FAIL: workitem-10198.chalice
# FAIL: workitem-10199.chalice
# FAIL: workitem-10200.chalice
# FAIL: workitem-8234.chalice
# FAIL: workitem-8236.chalice
# FAIL: workitem-9978.chalice
# ------------------------------------------------------
# SUMMARY: 58 of 62 tests failed.
# """