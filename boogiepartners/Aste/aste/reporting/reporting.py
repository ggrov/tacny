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
Functionality related to creating reports.

.. todo::
    Creating the report mail is currently rather inflexible and also distributed
    across various modules, for example, this one, run.py, reporting/boogie.py,
    tasks/boogie.py.
    
    My ideas for refactoring the reporting functionalities are
    
     - Define a set of environment data that is generic to all build processes
       and can thus be used regardless of the actual task that has been
       executed.
       
     - For each module in tasks/, e.g. for tasks/boogie.py, optionally define a
       report generator method (with a specific name) that can generate
       report text regardless of the executed task in that module.
       
     - For each task, e.g. tasks.boogie.FullBuild, optionally define a report
       generator method (with a specific name) that generates report text
       specific to the executed task.
       
     - After a task has been executed the most specific generator that is
       actually is declared is called. If none is present, the default one
       is used. If a specific generator method exists then the less specific
       ones are not called. It is thus left to the implementor of the specific
       method to invoke the less specific ones, if appropriate.
       
     - The existing formatters for e.g. BuildExceptions can probably be kept as
       they currently are.
       
     - Not addressed yet: how to disable reporting on a task-specific basis
"""

import textwrap

def _concat(what, to):
    if not what.endswith('\n'):
        what += '\n'

    if to:
        to += "\n" + what
    else:
        to = what

    return to


class AsteExceptionFormatter(object):

    WIDTH = 80

    INDENT = '  '

    __main_wrapper = None

    __dict_wrapper = None

    __text = ""

    def __init__(self):
        self.__main_wrapper = textwrap.TextWrapper(
            width=self.WIDTH, initial_indent=self.INDENT,
            subsequent_indent=self.INDENT)

        self.__dict_wrapper = textwrap.TextWrapper(
            width=self.WIDTH - len(self.INDENT),
            initial_indent=self.INDENT, subsequent_indent=self.INDENT * 3)

    def format(self, exc):
        self.__text = ""

        self.__add_if_set("Error context", exc.context)
        self.__add_if_set("Error message", exc.message)
        self.__add_subdict_if_set(exc.message_values)

        if not self.__text.endswith("\n"):
            self.__text += "\n"

        return self.__text

    def __add_subdict_if_set(self, dictionary):
        pairs = ""

        if len(dictionary) > 0:
            pairs = ["%s=%s" % (k, v) for k,v in dictionary.items()]
            pairs = "\n".join([self.__dict_wrapper.fill(pair) for pair in pairs])

            self.__text += "\n\n" + pairs

    def __add_if_set(self, title, text):
        if text:
            textlines = text.split('\n')
            maxlen = max([len(line) for line in textlines])

            if maxlen > self.WIDTH:
                text = self.__main_wrapper.fill(text)
            else:
                text = "\n".join(["  " + line for line in textlines])

            self.__text = _concat("%s:\n%s" % (title, text), self.__text)
            

# TODO: 2011-08-17 Malte:
#   The generator doesn't actually have state and it might be better to have a
#   bunch of (static) methods taking IDENT and env as arguments, which
#   generate parts of the report that are combined afterwards.
#   This is partly done already by having such methods in e.g.
#   reporting/specsharp.py.
class BuildStatusReportGenerator(object):

    INDENT = AsteExceptionFormatter.INDENT

    __text = ""

    __env = None

    def __init__(self, env):
        self.__env = env

    def generate_report(self):
        self.__text = ""

        projects_data = self.__env.data['projects']

        for project in projects_data:
            data = projects_data[project]

            text = project + ":"
            text = self.__append_revision_if_set(text, project)
            text += "\n"

            if not data['build']['started']:
                text += self.INDENT + "Build not performed\n"
            else:
                if data['build']['success'] and not data['tests']['failed']:
                    text += self.INDENT + "OK\n"
                else:
                    if not data['build']['success']:
                        text += self.INDENT + "Build failed\n"
                    else:
                        # Tests must have failed.
                        text += "%s%s %s\n" % (self.INDENT,
                            len(data['tests']['failed']), "test(s) failed")

            if project in self.__env.data['commits']:
                text += self.INDENT + "Summary changed\n"

            self.__text = _concat(text, self.__text)

        return self.__text

    def generate_report_summary(self, exception=None):
        projects_data = self.__env.data['projects']

        summary = ''
        tests_failed = False
        summaries_comitted = len(self.__env.data['commits']) != 0

        for project in projects_data:
            tests_failed = tests_failed or projects_data[project]['tests']['failed']

        pieces = []
        if exception: pieces.append(exception.__class__.__name__)
        if tests_failed: pieces.append('Tests failed')
        if summaries_comitted: pieces.append('Summaries committed')

        if pieces:
            summary = ", ".join(pieces)
        else:
            summary = 'OK'
            
        if self.__env.data['taskname']:
            summary = self.__env.data['taskname'] + ": " + summary

        return summary

    def __append_revision_if_set(self, text, project):
        data = self.__env.data
        key1 = 'CheckoutWorker'
        key2 = 'get' + project
        
        if (key1 in data and key2 in data[key1]):
            text += " " + data[key1][key2]['last_changed_revision']

        return text


# Methods that generate parts of the report and can be combined in arbitrary
# order.

def generate_summary_file_urls_from_config(indent, env):
    text = ''

    for project in env.data['projects']:
        if (project in env.cfg.CommitSummary
                and 'Url' in env.cfg.CommitSummary[project]):

                text += indent + "%s summary: %s\n" % (
                        project, env.cfg.CommitSummary[project].Url)
                        
    return text