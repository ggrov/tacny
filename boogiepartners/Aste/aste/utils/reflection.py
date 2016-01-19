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
http://code.activestate.com/recipes/223972-import-package-modules-at-runtime/

**Attention:** Only import the module, **not** its members, i.e.
don't use ``from reflection import *``!
"""


def get_mod(modulePath):
  return __import__(modulePath, globals(), locals(), ['*'])

def get_func(fullFuncName):
  """Retrieve a function object from a full dotted-package name."""

  # Parse out the path, module, and function
  lastDot = fullFuncName.rfind(u".")
  funcName = fullFuncName[lastDot + 1:]
  modPath = fullFuncName[:lastDot]

  aMod = get_mod(modPath)
  aFunc = getattr(aMod, funcName)

  # Assert that the function is a *callable* attribute.
  assert callable(aFunc), u"%s is not callable." % fullFuncName

  # Return a reference to the function itself,
  # not the results of the function.
  return aFunc

def get_class(fullClassName, parentClass=None):
  """Load a module and retrieve a class (NOT an instance).

  If the parentClass is supplied, className must be of parentClass
  or a subclass of parentClass (or None is returned).
  """
  aClass = get_func(fullClassName)

  # Assert that the class is a subclass of parentClass.
  if parentClass is not None:
      if not issubclass(aClass, parentClass):
          raise TypeError(u"%s is not a subclass of %s" %
                          (fullClassName, parentClass))

  # Return a reference to the class itself, not an instantiated object.
  return aClass
