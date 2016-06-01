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
Functionality related to reporting build process details specific to Boogie.
"""

def generate_additional_information(indent, env):
    text = "Additional information:\n"

    text += indent + "Host id: %s\n" % env.cfg.HostId
    text += indent + "Tests (only short): %s (%s)\n" % (
                        env.cfg.Flags.Tests, env.cfg.Flags.ShortTestsOnly)
    text += indent + "Commit summary (if changed): %s\n" % env.cfg.Flags.UploadSummary
    text += indent + "Upload build: %s\n" % env.cfg.Flags.UploadTheBuild

    return text