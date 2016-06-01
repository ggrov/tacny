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
Configuration parser(s) used by Aste to read its configuration file.
"""

# 3d-party module 'config'
# aka 'HierConfig' http://wiki.python.org/moin/HierConfig
import config


class ConfigWrapper(object):
    """A wrapper partially mimicing the interface of
    :class:``ConfigParser.ConfigParser``.
    An implementation of this interface is free to choose whatever configuration
    module suits best, e.g. the built-in ``ConfigParser`` or any 3rd-party
    library.

    A ``ConfigWrapper`` does not need to provide write-access to the configuration
    file, so that changes made to the configuration are written back to the
    configuration file.
    """

    def read(self, filename, *params): pass
    """Reads a configuration from the file ``filename`` and processes it, such
    that :func:`asObject` can be called afterwards. `*params` can be any
    additional parameters to this class or to the underlying configuration parser.
    """

    def asObject(self): pass
    """Returns the parsed configuration as a *property object*, i.e. one that
    allows access to the parsed configuration by field access syntax, e.g.::

        if cfg.aCategory.anOption == 'yes' and cfg.isActivated:
            print cfg.bounds.max * 2
    """


class ConvenientConfig(ConfigWrapper):
    """, but internally using the ``config`` module found
    at http://www.red-dove.com/config-doc/ to parse a JSON-style config file.

    Assuming this configuration in 'main.cfg':

    .. code-block:: js

        Foo: {
            isSunny: False
            max: 22
        }

        Bar: {
            PathX: '/dev/null'
            PathMS: 'e:\\dir\\subdir'
        }

    the following will work:

    >>> parser = ConvenientConfig()
    >>> parser.read('test.cfg')
    ['test.cfg']
    >>> cfg = parser.asObject()
    >>> type(cfg.Foo.isSunny)
    <type 'bool'>
    >>> type(cfg.Foo.max)
    <type 'int'>
    >>> type(cfg.Bar.Path)
    <type 'str'>

    See the documentation of the ``config`` for much more advanced examples.
    """

    cfg = None

    def read(self, filename, *params):
        """``*params`` are passed as additional parameters to ``config.Config``.
        """
        with open(filename) as fh:
            self.cfg = config.Config(fh, *params)

    def asObject(self):
        """Simply returns the ``config.Config`` object containing the configuration.
        """
        return self.cfg
