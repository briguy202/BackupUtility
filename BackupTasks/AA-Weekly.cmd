cd Regular
call Documents.cmd
call Desktop.cmd
REM Installs haven't been unzipped, so skipping them
REM call Installs.cmd
call Photos.cmd
call Movies.cmd

REM Be sure to run the Archive scripts last since they will depend on what happens above for archiving.
REM NOTE: The photos archive is going to the external drive, not to D:
cd ..\Archive
call AA-Archive.cmd

cd ..\Extended
call AA-ArchiveExtended.cmd