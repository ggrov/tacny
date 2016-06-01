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
Miscellaneous utility tasks not specific to a single build process.
"""

from aste.tasks.tasks import Task
from aste.workers.svnworkers import CommitSummaryWorker

class DiffLogs(Task):
    def run(self, **kwargs):
        worker = CommitSummaryWorker(self.env)
        diff = worker.diff(kwargs['file1'], kwargs['file2'])

        print diff

class Noop(Task):
    def run(self): pass
        # Does nothing