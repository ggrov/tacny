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
Miscellenous workers.
"""

import csv
from aste.workers.workers import BaseWorker
from aste.workers.resultworkers import TimingsRecorder

class TimingsCSVExporter(BaseWorker):

    def _format(self, category, operation):
        return '%s::%s' % (category, operation)

    def export(self, destination):
        """
        .. todo:: Break into several smaller methods.
        """

        records = TimingsRecorder(self.env).loadExistingRecords()

        #
        # http://openbook.galileocomputing.de/python/python_kapitel_19_005.htm
        # has some helpful examples how to work with the csv module.
        #

        with open(destination, 'w') as fh:
            # Create a list 'operations' containing all tests from all categories.
            # Each test is prefixed with its category to avoid name clashes.
            # E.g. the following two (stripped) timing records
            #        'catA': {'test1', 'testfoo'}
            #        'catB': {'test1', 'gui'}
            # are transformed into the list
            #        ['catA::test1', 'catA::testfoo', 'catB::test1', 'catB::gui']
            operations = []
            categories = records.keys()

            for cat in categories:
                for time in records[cat]:
                    ops = records[cat][time].keys()
                    operations += map(lambda op: self._format(cat, op), ops)

            operations.sort()

            # Create a list of csv columns by adding 'Timestamp' as the first column
            # name to the list of operations and initiate the csv writer.
            columns = ['Timestamp'] + operations
            writer = csv.DictWriter(fh, columns, dialect=csv.excel)

            # Assemble the header row, i.e. create a data row such as this:
            # {'Timestamp': 'Timestamp', 'catA::opA': 'catA::opA', 'catB::opB': 'catB::opB'}
            header = {}
            for key in columns:
                header[key] = key

            writer.writerow(header)

            # Collect all timestamps present in any of the records.
            timestamps = set()
            for cat in categories:
                timestamps = timestamps.union(records[cat].keys())

            # Create an ordered list of collected timestamps.
            timestamps = list(timestamps)
            timestamps.sort()

            # Assemble the csv rows that contain the elapsed times per operation.
            # Each row corresponds to a single timestamp. Operations that have not
            # been timed at a given timestamp are represented as empty cells in the
            # final csv file.
            rows = []
            for time in timestamps:
                row = {'Timestamp': time}

                # Iterate over categories and the operations within.
                for cat in categories:
                    for op in records[cat]:

                        # Ensure that the current category contains the current timestamp.
                        if time in records[cat]:
                            record = records[cat][time]

                            # Create a cell per operation.
                            for op in record:
                                column = self._format(cat, op)
                                row[column] = record[op]

                rows.append(row)

            # Write the data to the csv file.
            writer.writerows(rows)

            self.note('Exported %s csv rows to %s' % (len(rows), destination))
