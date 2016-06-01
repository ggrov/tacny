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

from aste.workers.workers import BuildWorker, accept, raiseNonBuildError, acceptIfNotZero, abortIfSevere

class MSBuildWorker(BuildWorker):
    def __init__(self, env, project_name):
        super(MSBuildWorker, self).__init__(env, project_name)
        
    matchers = {
        'general': [
            (['warning \w+: .*', 'NMAKE : fatal error \w+: .*'], [accept], [str]) # TODO(wuestholz): Why should we accept a fatal error?
        ],
        'counting': [
            (
                [ # Patterns
                    '((\d+) (error)\(s\)),', '((\d) (warning)\(s\))',
                    '(, (\d+) (warning)s)', '(-- (\d+) (error)s,)', '(, (\d+) (failed),)'
                ],
                [ # Filters
                    lambda match: acceptIfNotZero(match[1]),
                    lambda match: abortIfSevere(match[2])
                ],
                # Formatter
                [lambda match: "%s %s" % (match[1:3])]
            )
        ],
        'envfatals': [
            (
                ['\d+>?(ERROR copying)'],
                [raiseNonBuildError], [lambda match: match[0]]
            ), (
                ['The process cannot access the file because it is being used by another process'],
                [raiseNonBuildError], [lambda match: match[0]]
            )
        ],
        #
        # http://blogs.msdn.com/b/msbuild/archive/2006/11/03/msbuild-visual-studio-aware-error-messages-and-message-formats.aspx
        #
        'msbuild-friendly': [
            ([r"""
(?imx) # re.IGNORECASE, re.MULTILINE, re.VERBOSE
^(
    (?:                    # Origin (optional)
        (?:
            ([A-Z]:\\.*?)\((.*?)\)    # Absolute path followed by (line,column)
                |
            (.*?)                     # Or simply anything
        )(?::\ )                # followed by ": "
    )?
    [^:]?                  # Subcategory (optional)
    (error|warning)\       # Category (required)
    (\w+)                  # Code (required)
    (:\ .*)?               # Text (optional)
)$
"""], [accept], [lambda match: match[0]]
            )
        ]
    }
    
    def _matchDefaults(self, output):
        """Matches the ``output`` with the matcher groups
        *msbuild-friendly*, *envfatals*, *general* and *counting*, and returns
        the matches in a single list.
        """

        matches = self.matchNamedGroup('msbuild-friendly', output)
        matches += self.matchNamedGroup('envfatals', output)
        matches += self.matchNamedGroup('general', output)
        matches += self.matchNamedGroup('counting', output)

        return matches