"..\..\BackupUtility\BackupUtility.exe" -jobName Movies -configFile "..\AA-config.xml" -sourceDir "C:\Shared\Movies\Family Movies" -destDir "D:\Backup\FamilyMovies"
"..\..\BackupUtility\BackupUtility.exe" -jobName MoviesCopy -configFile "..\AA-config.xml" -sourceDir "D:\Backup\FamilyMovies" -destDir "{DRIVE}:\Backup\Regular\FamilyMovies" -copies 3 -useCompression false -sourceCopyNewest -requiredDriveName HibmaExternal