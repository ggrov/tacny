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
Functionality related to sending mails

.. code-block:: python

        sender = 'Sir John <john@royal.com>'
        recipients = ['James <james@service.com>']
        subject = 'Running out of champagne!'

        message = mail.createMessage(sender, recipients, subject, '')

        mail.sendMail(sender, recipients, message, 'localhost', 586,
                      'john', '1234', true)
"""


from smtplib import SMTP
from email.mime.text import MIMEText
from email.header import Header
from email.utils import COMMASPACE


def createMessage(sender, recipients, subject, body):
    """Creates and returns a :class:`email.message.Message` to be send via
    :class:`smtplib.SMTP`.

    Only the real name part of the sender the recipients addresses may contain
    non-ASCII characters.

    The email will be send in UTF-8 and properly MIME encoded.
    """

    header_charset = 'UTF-8'
    body_charset = 'UTF-8'

    # Create the message ('plain' stands for Content-Type: text/plain)
    msg = MIMEText(body.encode(body_charset), 'plain', body_charset)
    msg['From'] = sender
    msg['To'] = COMMASPACE.join(recipients)
    msg['Subject'] = Header(unicode(subject), header_charset)

    return msg

def sendMail(sender, recipients, message, host="localhost", port=25,
        user=None, password=None, useTLS=False):
    """Sends the message (of type :class:`email.message.Message`) using the
    given mail server information.
    """

    conn = SMTP(host, port)

    # TLS encryption as required by e.g. Googlemail and the ETH mail server.
    if useTLS:
        conn.ehlo()
        conn.starttls()
        conn.ehlo()

    if user:
        conn.login(user, password)

    conn.sendmail(sender, recipients, message.as_string())
    conn.quit()
