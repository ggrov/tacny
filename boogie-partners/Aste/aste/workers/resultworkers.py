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


import json
import datetime
import re
import os
import aste.utils.mail as mail
from aste.utils.misc import ensure_directories_exist, rot47
from aste.workers.workers import BaseWorker
from suds.client import Client
from suds import WebFault


class StatusMailer(BaseWorker):

    def sendError(self, subject='', body=''):
        sbj = '[Aste] Error'
        if subject: sbj = '%s: %s' % (sbj, subject)

        self.send(sbj, body)

    def sendSuccess(self, subject=''):
        with open(self.cfg.Logging.SummaryLog) as fh:
            body = ''.join(fh.readlines())

        sbj = '[Aste] Success'
        if subject: sbj = '%s: %s' % (sbj, subject)

        self.send(sbj, body)

    def send(self, subject, body, subject_prefix='[Aste] '):
        subject = subject_prefix + subject

        sender = self.cfg.Mail.Sender
        recipients = self.cfg.Mail.Recipients

        message = mail.createMessage(sender, recipients, subject, body)

        mail.sendMail(sender, recipients, message, self.cfg.Mail.Host,
                      self.cfg.Mail.Port, self.cfg.Mail.User,
                      rot47(self.cfg.Mail.Password), self.cfg.Mail.TLS)

        self.note('Sent mail to %s with subject "%s"' % (recipients, subject))


class TimingsRecorder(BaseWorker):

    def loadExistingRecords(self):
        records = {}

        try:
            records = self._loadExistingRecords(self.cfg.Timings.JSON)
        except IOError:
            pass

        return records

    def _loadExistingRecords(self, filename):
        """
        Record format::

            {
                'operation 1': {
                    '2007-03-03 18:14:39': 3.1
                    '2007-03-03 20:14:39': 2.0
                    '2007-03-03 22:14:39': 2.5
                },
                'operation 2': {
                    '2007-03-03 18:14:39': 150.71
                    '2007-03-03 20:14:39': 100
                    '2007-03-03 22:14:39': 102.123
                }
            }
        """
        with open(filename, 'r') as fh:
            records = json.load(fh)

        return records

    def add(self, record):
        """
        Record format::

            record = {
                'timestamp': '2007-03-03 22:14:39', # ISO 8601
                'timings': {                        # Timings of the actual operations
                    'operation 1': 2.5,             # in seconds
                    'operation 2': 102.123,
                }
        """

        timestamp = record['timestamp']
        records = self.loadExistingRecords()

        for operation in record['timings']:
            elapsed = record['timings'][operation]

            if operation not in records:
                records[operation] = {}

            records[operation][timestamp] = elapsed

        self.record(records)
        self.note('Added %s timing record(s)' % len(record['timings']))

    def record(self, records):
        self._record(records, self.cfg.Timings.JSON)

    def _record(self, records, filename):
        ensure_directories_exist(filename, with_filename=True)

        with open(filename, 'w') as fh:
            json.dump(records, fh, sort_keys=True, check_circular=False,
                      indent=2)


class ReleaseUploader(BaseWorker):

    default_description = ("This download category contains automatically "
                         + "released nightly builds, reflecting the current "
                         + "state of Boogie's development. They are intended "
                         + "for experimental use only. Please download the "
                         + "Recommended Download to obtain the most recent "
                         + "release. The Other Available Downloads are in "
                         + "ascending order with the most recent release at "
                         + "the bottom of the list.")

    default_release_name = 'Nightly builds'

    client = None

    def __init__(self, env):
        super(ReleaseUploader, self).__init__(env)

        self.client = Client(self.cfg.Nightlies.SoapUrl)
        self.client.set_options(timeout=300)

    def create_release(self, project_name, username, password, release_name,
                       release_description=None,
                       status='Planning', show_to_public=True,
                       is_default_release=False):

        if release_description == None:
            release_description = self.default_description

        release_date = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

        result = self.client.service.CreateARelease(
            project_name, release_name, release_description,
            release_date, status, show_to_public, is_default_release,
            username, password)

        return result

    def upload_release(self, project_name, revision_number, username, password,
                     local_filename, remote_filename=None,
                     remote_linktext=None, release_name=None):

        """
            .. todo::
                The exception message might contain passwords which should be
                removed before the message is reraised.
        """

        release_date = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

        if release_name == None:
            release_name = self.default_release_name

        try:
            self.create_release(project_name, username, password, release_name)
        except WebFault as exc:
            pattern = (r"^Server raised fault: "
                       + r"'The release '(.*)' already exists\.'$")

            match = re.search(pattern, exc.args[0], re.MULTILINE)

            if match != None:
                msg = "WebFault raised as expect: release '%s' already exists."
                msg = msg % match.groups(1)
                self.note(msg)
            else:
                raise

        release_file = self.client.factory.create('ReleaseFile')

        # Split up filename (e.g., 'nightly-build.zip' into ('nightly-build', '.zip')).
        basename, ext = os.path.splitext(local_filename)

        # Set the link text of the download.
        if remote_linktext == None:
            release_file.Name = "{0}_rev{1}_{2}{3}".format(
                basename, revision_number, release_date, ext).encode("utf-8")
        else:
            release_file.Name = remote_linktext

        # Set the filename of the download.
        if remote_filename == None:
            release_file.FileName = local_filename
        else:
            release_file.FileName = remote_filename

        with open(local_filename, 'rb') as fh:
            fc = fh.read()

        release_file.FileType = 'RuntimeBinary'
        release_file.FileData = fc.encode('base64')

        files = self.client.factory.create('ArrayOfReleaseFile')
        files.ReleaseFile = [release_file]

        # Upload the release files.
        result = self.client.service.UploadTheReleaseFiles(
            project_name, release_name, files, release_file.FileName, username,
            password)

        self.note('Released %s as %s' % (local_filename, release_file.Name))

        return result
