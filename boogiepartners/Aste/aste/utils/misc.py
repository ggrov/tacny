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
Miscellaneous utility functions.
"""

import os


def rot47(string):
    """String encoding to / decoding from rot47. The code is taken from
    http://www.python-blog.com/2009/10/20/rot47-in-python/.
    """

    return ''.join([
            chr(33+((ord(c)-33+47) %(47*2))) if c != " " else " " for c in string
    ])

def ensure_directories_exist(path, with_filename=False):
    """
    If the last part of the ``path`` is a filename, pass ``with_filename=True``,
    otherwise a directory with that name will be created, unless there already
    is a file with that name.
    """

    if not with_filename:
        path = path + os.sep

    directoriesOnly = os.path.dirname(path)

    if not os.path.exists(directoriesOnly):
        os.makedirs(directoriesOnly)

