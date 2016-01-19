import re
import pprint

#with open("verbose -- some errors.log") as fhin:
with open("verbose -- NMAKE error.log") as fhin:
    text = fhin.read()

#
# http://blogs.msdn.com/b/msbuild/archive/2006/11/03/msbuild-visual-studio-aware-error-messages-and-message-formats.aspx
#

pattern = r"""
^(
    (?:                    # Origin (optional)
        (?:
            ([A-Z]:\\.*?)\((.*?)\)    # Absolute path followed by (line,column)
                |
            (.*?)                     # Or simply anything
        )(?::\ )                # followed by ": "
    )?
    [^:]?                    # Subcategory (optional)
    (error|warning)\         # Category (required)
    (\w+)                  # Code (required)
    (:\ .*)?               # Text (optional)
)$
"""

#pattern = 'NMAKE : fatal error \w+:.*'

matches = re.findall(pattern, text)



for m in matches:
    pprint.pprint(m)