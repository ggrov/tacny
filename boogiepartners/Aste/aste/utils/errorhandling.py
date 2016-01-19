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
Error handling functionality
"""


from aste.aste import AsteException


#
# The original code as taken from
#     http://code.activestate.com/recipes/408937-basic-exception-handling-idiom-using-decorators/
# has been modified significantly by studying code from
#    http://blog.ianbicking.org/2008/10/24/decorators-and-descriptors/
def exc_handler(exceptions, handler, **handler_kwargs):
    """
    This is a decorator!
    """

    try:
        exceptions = tuple(exceptions)
    except TypeError:
        # We assume that only a single exception type is given.
        exceptions = tuple([exceptions])

    if any(not issubclass(clazz, Exception) for clazz in exceptions):
        raise TypeError("Tuple must contain Exceptions only.")

    class ExceptionHandler(object):
        def __init__(self, func):
            self.func = func

        def __call__(self, *args, **kwargs):
            try:
                return self.func(*args, **kwargs)
            except exceptions, ex:
                handler(ex, **handler_kwargs)

        def __get__(self, obj, type=None):
            if obj is None:
                return self
            new_func = self.func.__get__(obj, type)
            return self.__class__(new_func)

    return ExceptionHandler


def add_context_to_aste_exception(exc, context=""):
    exc.context = context

    raise # Raise the current exception without changing its context.

def add_context(context):
    """
    This is a decorator!
    """

    return exc_handler(AsteException, add_context_to_aste_exception,
                       context=context)
