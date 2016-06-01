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
.. todo:: Check if all requirements are met (Visual Studio, Z3, etc.).

.. todo::
        Create a Python install package from Aste (an egg) that ideally also
        installes the cli SVN client, Sphinx, etc.
"""

import logging
import logging.handlers
from datetime import datetime
from utils.asteconfig import ConvenientConfig as ConvenientConfigParser
from utils.misc import ensure_directories_exist
from collections import OrderedDict

STATUS_OK = 0
STATUS_ERROR = 100


class Container(object):
    """An empty class that can be used to create property objects."""
    pass


class Fake(object):
    def __getattr__(self, key):
        return self

    def __call__(self, *args, **kwargs):
        return True


class AsteException(Exception):
    """
    Base class for Aste exceptions and errors.

    In general, AsteExceptions should only be raised **after** all useful
    information have been logged, so that exceptions catchers do not have to
    log the content of the catched exception.
    """

    __message = ""
    __context = ""
    __message_values = {}

    def __init__(self, message, context=""):
        super(AsteException, self).__init__(message)
        self.__message = message
        self.__context = context

    @classmethod
    def from_exc(self, aste_exc):
        self.__init__(aste_exc.message, context=aste_exc.context)
        self.add_message_values(aste_exc.message_values)

    def __str__(self):
        return "Context:\n  %s\nMessage:\n  %s" % (self.__context, self.__message)

    def set_message_value(self, key, value):
        self.__message_values[key] = value

    def add_message_values(self, dictionary):
        self.__message_values.update(dictionary)

    @property
    def message(self):
        return self.__message

    @property
    def message_values(self):
        return self.__message_values

    @property
    def context(self):
        return self.__context

    @context.setter
    def context(self, value):
        self.__context = value


class BuildError(AsteException):
    """
    A build error should be raised to indicate that a step in the build
    process failed and that the developers of the software currently being build
    should be informed about this.
    """
    pass


class NonBuildError(AsteException):
    """A non-build error should be raised to indicate that a step in the build
    process failed, but due to reasons that are of no concern to the developers
    of the software currently being build.

    Thus, the error should only be reported to the developers of Aste and to the
    administrator of the machine running Aste.
    """
    pass


class Environment(object):
    """ Initialises and stores the Aste environment: read the configuration from
    ``options.configFile`` and creates the loggers for verbose and for
    summary output.

    The verbose logger is intended to log every output produced during the build
    process, while the summary logger should only log non-success status
    summaries (``warning CS123``, ``3 failed``, etc.) and failed tests.

    To be more specific:

        - if everything is fine, the summary should just contain the test results

        - if anything is suspicious, the summary should include also that, but as
          short as possible

        - if a real error is found (one that is not just suspicious and currently
          ignored because the build seems to work fine), the error must be reported
          (error to verbose log, to summary log, maybe send a mail)
          and the build process should abort.

    ``options.noFileLogging`` is handled by :func:`__init_logger`.
    """

    __cfgObject = None
    __verboseLogger = None
    __summaryLogger = None

    data = {
        'taskname': None, # String, optional
        'taskclass': None, # Class, to be set during initial setup
        'configuration': None, # String, to be set during initial setup
        'status': STATUS_OK,
        'timings': {
            'timestamp': datetime.now().strftime('%Y-%m-%d %H:%M:%S'),
            'timings': {}
        },
        'commits': [],
        'projects': OrderedDict()
    }
    """A dictionary storing arbitrary data produced while running Aste.

    ..todo::
            Include default values in this description, maybe by using an
            expression evaluation directive/plugin of Sphinx.
    """

    def __init__(self, options):
        """ Creates a new environment according to the ``options``.
        """
        self.__init_config(options.config)
        self.__init_logger(options.noFileLogging)
        self.__log_summary_header()

    def __init_config(self, configFile):
        """ Reads ``configFile`` and creates the corresponding property object
        accessible via :func:`cfg`.
        """
        cfg = ConvenientConfigParser()
        cfg.read(configFile)

        cfgObject = cfg.asObject()
        self.__cfgObject = cfgObject

    def __init_logger(self, noFileLogging):
        """ Initialises the verbose and the summary logger. Both will rotate over
        the amount of log files configured in ``Logging.Backups``.

        If ``options.noFileLogging`` is true

            - the verbose logger will only log to the console
            - the summary logger is replaced by a dummy object such that
                calls to it always return true

        .. todo:: Replace logger faking by something less hacky. Maybe we can
                simply remove the handlers to deactivate the logger calls.
                See http://docs.python.org/library/logging.html?highlight=logging#configuring-logging-for-a-library
        """

        # Create the verbose logger and have it log to a console.
        verboseLogger = logging.getLogger('SpecSharp.Verbose')
        verboseLogger.setLevel(logging.DEBUG)

        console = logging.StreamHandler()
        verboseLogger.addHandler(console)

        if noFileLogging:
            # Replace the summary logger by a fake object. This probably
            # is not the safest way of disabling the summary logger.
            summaryLogger = Fake()
        else:
            ensure_directories_exist(self.cfg.Logging.VerboseLog,
                                                with_filename=True)
            ensure_directories_exist(self.cfg.Logging.SummaryLog,
                                                with_filename=True)

            # Have the verbose logger also log to a rotating file.
            logfile = logging.handlers.RotatingFileHandler(self.cfg.Logging.VerboseLog,
                    backupCount=self.cfg.Logging.Backups)
            logfile.doRollover() # Use next logfile
            verboseLogger.addHandler(logfile)

            # Create the summary logger and have it log to a rotating file.
            summaryLogger = logging.getLogger('SpecSharp.Summary')
            summaryLogger.setLevel(logging.DEBUG)
            logfile = logging.handlers.RotatingFileHandler(self.cfg.Logging.SummaryLog,
                    backupCount=self.cfg.Logging.Backups)
            logfile.doRollover()
            summaryLogger.addHandler(logfile)

        self.__verboseLogger = verboseLogger
        self.__summaryLogger = summaryLogger

    def __log_summary_header(self):
        now = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        self._summary.info('# Aste started: ' + now)
        self._summary.info('# Host id: ' + self.cfg.HostId)

    @property
    def cfg(self):
        """ The configuration as returned by :func:`ConvenientConfigParser.asObject`.
        """
        return self.__cfgObject

    @property
    def _verbose(self):
        """The verbose logger."""
        return self.__verboseLogger

    @property
    def _summary(self):
        """ The summary logger."""
        return self.__summaryLogger

