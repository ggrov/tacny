.. _notes:

Notes
=====

* ``z3.exe`` must either be in the path or it must be copied to the checkout
  folders ``Boogie\Binaries`` and ``SscBoogie\Binaries``.

* The term *build data* refers to the environment data stored at ``env.data``.
  A *data path* such as '/foo/bar/baz' actually means that the data is stored
  in ``env.data[foo][bar][baz]``.

* A ``BuildError`` should only be raised after handling the
  erroneous situation, e.g. after
  - setting the environment fields ``status`` and ``error`` to appropriate values
  - logging the error

  If a BuildError is raised without actually handling the situation, it should
  be caught (and probably re-raised afterwards) in the same class.

* A ``NonBuildError`` should be raised to indicate that an exception occurred
  that is (mostly likely) unrelated to the software that currently is being
  build.

  Hence, NonBuildErrors should suppress summary file commits and they should
  eventually reach ``run.py`` where they trigger an error mail.

* The logging methods of the ``BaseWorker``
  - ``log``
  - ``note``
  - ``logSummary``
  - ``noteSummary``
  - ``reportError``

* The ``run*`` (execute a command) methods of the ``BaseWorker``
  - ``run``
  - ``runObserved``
  - ``runSafely``

* I sometimes get "access denied" errors from the SVN client on Windows 7 when
  trying to manually (from the command line) run ``run.py`` operating on the
  checkout folders that initially have been created when ``run.py`` has been
  run as a Windows task (by the Windows task scheduler).

  I did not get similiar errors when manually starting the scheduled task from
  the command-line via ``schtasks /Run /TN <taskname>``.
  
* If a local user is used to run the scheduled Windows Task, make sure that
  the necessary Python modules are available for that user. In my case the ``suds``
  package wasn't available initially, but it was after I added the local user to
  the ``Administrators`` group.

Dev Links
---------

- http://www.red-dove.com/config-doc/
- http://docs.python.org/library/email-examples.html

- http://stackoverflow.com/questions/894088/how-do-i-get-the-current-file-current-class-and-current-method-with-python
