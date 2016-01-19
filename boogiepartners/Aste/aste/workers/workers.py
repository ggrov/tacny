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
        Add sub-groups to the matchers that are invoked depending on the parent
        group, e.g. when an exception is raised or when a match passes all
        filters. This would allow to search for e.g.

            Done building project "Microsoft.SpecSharp.csproj" -- FAILED.

        when "\d+ failed" is matches.

.. todo:: Move global function (accept, ...) into a class.

.. todo:: Unify worker names, i.e. either suffix them with Worker or not.

.. todo::
        Extract log() and note() and move them into the Environment.
        Currently, if a Task wants to log something, it either has to
        utilise a worker for that or use env.loggers, but that way the
        output is not formatted as alike as the normal log entries.
"""


import sys
import os
import re
from collections import OrderedDict
from subprocess import (Popen, PIPE, STDOUT)
from datetime import datetime
from aste.reporting.reporting import AsteExceptionFormatter
from aste import aste # Dangerous!


class BaseWorker(object):
    """Implements basic functionality common to all steps executed during the
    build process such as the execution of shell scripts and logging. The logger
    and the configuration to work with is taken from the environment ``env``.
    """

    prefix = "\n\n"
    """Default prefix, used by :func:`note`"""

    _env = None
    """The build environment."""

    def __init__(self, env):
        self._env = env

    @property
    def cfg(self):
        """Configuration as provided by the environment."""
        return self.env.cfg

    @property
    def env(self):
        """The environment in which the worker operates."""
        return self._env

    def cd(self, destDir):
        """Changes the working directory to ``destDir``."""
        self.note("chdir %s" % destDir)
        os.chdir(destDir)

    def sys(self, cmd, shell=False):
        """Executes ``cmd`` in the current working directory and returns a
        dictionary with the ``returncode`` and the ``output``, i.e.
        the return code upon termination and the output written to stdout and stderr.

        While the execution takes place, the output is continuously logged
        by the verbose logger via :func:`log`.

        The ``shell`` parameter is passed to :class:`subprocess.Popen`.
        """

        output = ""
        proc = Popen(cmd, shell=shell, stdout=PIPE, stderr=STDOUT)
        line = 'not the empty string'

        # Read output and log it as long as the command is running.
        while line or proc.poll() is None:
            sys.stdout.flush()
            line = proc.stdout.readline().rstrip()

            # print 'line = #%s#, retcode=#%s#, %s, %s' % (
                    # line, proc.returncode, line == '', proc.poll())

            if line != '':
                self.log(line)
                output += line + os.linesep

        return {'returncode': proc.returncode, 'output': output}

    def run(self, cmd, logcmd=None, shell=False, note=True):
        """Executes ``cmd`` via :func:`sys` and returns its result dictionary.
        If ``note`` is true, it also logs the string ``logcmd``
        via :func:`note`.

        ``logcmd`` defaults to ``cmd``, but it can be overwritten in case the
        real command is not suited for logging, e.g. because it contains
        private information.
        """

        if logcmd == None: logcmd = cmd

        if note: self.note(logcmd)
        return self.sys(cmd, shell=shell)

    def runObserved(self, cmd, observer_func, **kwargs):
        """
        Executes ``cmd`` via :func:`run` and calls the observer function
        ``observer_func(cmd, returncode, output)`` if the return code is not
        equal to zero. The ``**kwargs`` are forwarded to ``run``.
        """

        result = self.run(cmd, **kwargs)
        if result['returncode'] != 0:
            observer_func(cmd, result['returncode'], result['output'])

        return result

    def runSafely(self, cmd, **kwargs):
        """
        Executes ``cmd`` via :func:`runObserved` (and passing the ``kwargs``
        to it) using :func:`_abortWithReturnCode` as the observer function.
        """

        result = self.runObserved(
            cmd,
            lambda cmd, return_code, output: self._abortWithReturnCode(
                cmd, return_code),
            **kwargs)

        return result

    def _abortWithReturnCode(self, cmd, return_code, **kwargs):
        """Aborts via :func:`abort` with an abort message containing ``cmd``
        and ``return_code``. Both values are added to the ``kwargs`` and
        forwarded to ``abort``.
        """

        msg = "%s failed with return code %s" % (cmd, return_code)

        kwargs['cmd'] = cmd
        kwargs['returncode'] = return_code

        self.abort(msg, **kwargs)

    def abort(self, message, context=None, exception_class=aste.BuildError,
              **kwargs):
        """
        Creates and raises an exception of class ``exception_class``
        containing the passed information ``message``, ``context`` and
        the ``kwargs`` as additional message values.
        The ``exception_class`` must be a subtype of :class:`AsteException`.

        Moreover, the error is formatted by a :class:`AsteExceptionFormatter`
        and the output is passed to :func:`reportError`.
        """

        error = exception_class(message, context=context)

        if kwargs:
            error.add_message_values(kwargs)

        logmsg = AsteExceptionFormatter().format(error)
        self.reportError(logmsg)

        self.env.data['status'] = aste.STATUS_ERROR
        self.env.data['error'] = error

        raise error

    def reportError(self, message):
        """
        Logs ``message`` as erroneous using the summary logger and the verbose
        logger.
        """

        logmsg = "[%s] %s" % ("Error", message)

        self.note(logmsg, logger=self.env._verbose)
        self.note(logmsg, prefix="", logger=self.env._summary)

    def note(self, msg, prefix=None, **kwargs):
        """Prefixes ``msg`` with the current date and logs that via :func:`log`.
        If ``prefix != None`` then ``prefix`` is prepended to the string passed
        to ``log``. ``**kwargs`` are forwarded to ``log``.
        """

        if prefix == None:
            prefix = self.prefix

        now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        msg = "%s[%s] %s" % (prefix, now, msg)

        self.log(msg, **kwargs)

    def log(self, msg, logger=None, **kwargs):
        """Logs ``msg`` via :func:`logging.Logger.info` using the logger
        provided (default: the verbose logger).
        ``**kwargs`` are forwarded to ``info``.
        """

        if logger == None:
            logger = self.env._verbose
        logger.info(msg, **kwargs)

    def noteSummary(self, msg, **kwargs):
        """Notes ``msg`` to the summary logger using "" as the prefix, if none is
        explicitly given.
        """

        if 'prefix' not in kwargs:
            kwargs['prefix'] = ''

        self.note(msg, logger=self.env._summary, **kwargs)

    def logSummary(self, msg, **kwargs):
        """Logs ``msg`` to the summary logger.
        """

        self.log(msg, logger=self.env._summary, **kwargs)


class MatchingWorker(BaseWorker):
    """Extends a ``BaseWorker`` by general output matching functionalities."""

    matchers = {}
    """A dictionary of match groups indexed by group name.
    Each group consists of a list of tuples, where each tuple consists of
    a name, a list of patterns, a list of filters and a
    list of formatters, e.g.::

        matchers = {
            'general': [ # group name
                ( # first tuple
                    ['warning CS\d+', 'NMAKE : fatal error .\d+:'], # patterns
                    [accept], # filter
                    [str] # formatter
                )
            ]
        }

    See :func:`matchNamedGroup` for information about how the matchers are used.
    """

    _errors = []
    """Collects BuildErrors raised during the execution of the output filters."""

    _matches = []

    def matchNamedGroup(self, key, output):
        """Matches the regular expressions in the group ``key`` against the given
        ``output`` by passing the corresponding group to :meth:`matchGroup` and
        returning the result.
        """

        group = self.matchers[key]

        return self.matchGroup(group, output)

    def matchGroup(self, group, output):
        """Matches the regular expressions in the matching ``group`` against the
        given ``output``.

        Each matcher group is processed by subsequently processing
        each tuple in the group as follows:

         - The tuple patterns are subsequently matched against a given output
           and the following steps are subsequently performed for each match
           resulting from a pattern:

         - The match is passed to each filter. If a filter

             - returns ``True`` the match is passed on to the next filter

             - returns ``False`` the match is rejected, i.e. it is not
               considered any further. In particular, it is not passed on to
               the next filter

             - raises a ``BuildError`` the match is accepted (and passed on
               to the next filter) and the error is stored in
               :attr:`self._errors` for later use

         - If all filters accepted the match, it is subsequently passed to each
           formatter (as in a pipeline)

         - The result of the formatter pipeline is finally appened to an
           output list and returned
        """

        out = [] # Accepted and formatted matches

        # Iterate over all patterns.
        for tup in group:
            for pattern in tup[0]:
                matches = re.findall(pattern, output)

                self._matches += matches

                # Iterate over all matches of the current pattern.
                for m in matches:
                    accepted = True

                    # Iterate over all filters to see if one of them rejects the
                    # match or raises a BuildError.
                    for filterfunc in tup[1]:
                        try:
                            if not filterfunc(m):
                                # Reject the match, continue with the next match.
                                accepted = False
                                break
                        except aste.BuildError as err:
                            # Store that error and continue filtering.
                            self._errors.append(err)

                    # If accepted, pass the match through a pipeline of formaters.
                    if accepted:
                        for formatter in tup[2]:
                            m = formatter(m)

                        out.append(m)

        return out


def accept(*args):
    """Always returns true, regardless of the arguments.
    """

    return True


def acceptIfNotZero(match):
    """Returns true if ``match`` is not equal to zero.
    """

    return int(match) != 0


def abortIfSevere(match):
    """
    Raises a ``BuildError`` if ``match`` is either ``error`` or ``failed``.
    """

    if match in ['error', 'failed']:
        raise aste.BuildError("Aborted due to severe match: " + str(match))


def raiseNonBuildError(match):
    raise aste.NonBuildError(str(match))


class BuildWorker(MatchingWorker):
    __project = None
    __project_data = None

    def __init__(self, env, project_name):
        print "project name:", project_name

        super(BuildWorker, self).__init__(env)

        self.__project = project_name
        self.__create_data_entry()

    def _matchDefaults(self, output):
        """Subsequently match ``output`` against match groups that are always
        to be used, i.e. that are don't depend on each other's results.
        """
        raise '%s.%s.%s is an abstract method but hasn\'t been overriden.' % (
                __class.__.__module__, __class__.__name__, _matchDefaults.__name__)

    def _runDefaultBuildStep(self, cmd):
        """
        Runs ``cmd`` and matches the output via :func:`_matchDefaults`.
        All matches are logged by the summary log.

        Returns a dictionary containing the ``resultcode`` and the ``output``
        of the executed command, as well as the ``matches`` found in the output.

        If :attr:`~MatchingWorker._errors` contains any errors, this is reported
        via :func:`~BaseWorker.reportError` and a fresh BuildError is raised.

        .. todo::
                It should be possible to get the name of the step method that
                (indirectly) called _matchDefaults. In case of an error the
                caller name could then be added to the logs to facilitate
                debugging.
        """

        result = self.run(cmd)

        matches = self._matchDefaults(result['output'])

        if len(matches) > 0:
            msg = "\n"
            msg += "\n".join(["    " + str(m) for m in matches])

            if len(self._errors) > 0:
                self.reportError(cmd)
                self.logSummary(msg)

                error = aste.BuildError("Found build errors.", self._errors)
                self.env.data['status'] = aste.STATUS_ERROR
                self.env.data['error'] = error

                raise error
            else:
                self.noteSummary(cmd)
                self.logSummary(msg)

        result['matches'] = matches

        return result

    def __create_data_entry(self):
        self.env.data['projects'][self.project] = {
            "build": {
                "started": False,
                "success": False,
                "data": {}
            },
            "tests": {
                "succeeded": [],
                "failed": []
            },
            "data": OrderedDict()
        }

        self.__project_data = self.env.data['projects'][self.project]

    @property
    def project(self):
        return self.__project

    @property
    def project_data(self):
        return self.__project_data