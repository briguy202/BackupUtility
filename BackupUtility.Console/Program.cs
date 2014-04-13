using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using CommonLibrary.Console;
using Microsoft.Win32.TaskScheduler;
using System.Threading;

namespace BackupUtility {
	class Program {
		private static ConsoleHelper _helper;
		private static SmtpClient _emailClient;
		private static MailMessage _emailMessage;
		private static bool _debugMode;

		#region Constants
		private const string WinRarPathConfigKey = "WinRARPath";
		private const string CONFIG_NAME = "jobName";
		private const string CONFIG_SOURCE_DIRECTORY = "sourceDir";
		private const string CONFIG_DESTINATION_DIRECTORY = "destDir";
		private const string CONFIG_CREATE_DIR_IF_NOT_EXIST = "createDestDir";
		private const string CONFIG_USE_COMPRESSION = "useCompression";
		private const string CONFIG_COPIES_TO_KEEP = "copies";
		private const string CONFIG_ON_START_DELETE_COPIES = "onStartDeleteCopies";
		private const string CONFIG_EMAIL_SEND_TEST = "emailSendTest";
		private const string CONFIG_EMAIL_HOST = "emailHost";
		private const string CONFIG_EMAIL_PORT = "emailPort";
		private const string CONFIG_EMAIL_SSL = "emailUseSSl";
		private const string CONFIG_EMAIL_FROM_ADDRESS = "emailFromAddress";
		private const string CONFIG_EMAIL_FROM_NAME = "emailFromName";
		private const string CONFIG_EMAIL_TO_ADDRESS = "emailToAddress";
		private const string CONFIG_EMAIL_TO_NAME = "emailToName";
		private const string CONFIG_FILE = "configFile";
		private const string CONFIG_DEBUG = "debug";
		private const string CONFIG_WAIT_FOR_TASKS = "waitForTasks";
		private const string CONFIG_WAIT_SECONDS = "waitSeconds";
		private const string CONFIG_RUN_IF_OLDER_THAN = "runIfOlderThan";
		private const string CONFIG_REQUIRED_DRIVE_LETTER = "requiredDriveLetter";
		private const string CONFIG_REQUIRED_DRIVE_NAME = "requiredDriveName";
		private const string CONFIG_SOURCE_COPY_NEWEST = "sourceCopyNewest";
		#endregion

		static void Main(string[] args) {
			try {
				_helper = new ConsoleHelper(new ArgumentCollection("-"));
				_helper.Arguments.HelpArgument = new Argument("?", "Prints the help/usage information screen.", false, false);
				_helper.Arguments.ConfigurationFileArgument = new Argument(CONFIG_FILE, "Specifies the location of the XML configuration file that can be used to store inputs to this program.", false, true);
				_helper.Arguments.Add(new Argument(CONFIG_WAIT_FOR_TASKS, "Comma-separated list of tasks that must be completed before this command will run. If a task is still running, this operation will sleep until it completes.", false, true));
				_helper.Arguments.Add(new Argument(CONFIG_WAIT_SECONDS, "The number of seconds a task should wait before waking up to see if pre-tasks are completed.", false, true) { DefaultValue = "60" });
				_helper.Arguments.Add(new Argument(CONFIG_DEBUG, "When this value is set, no actions are performed.  This is for debugging use only.", false, false));
				_helper.Arguments.Add(new Argument(CONFIG_NAME, "Specifies a unique name for this operation.  This is used in emails and trace statements to identify what operation is being run.", true, true));
				_helper.Arguments.Add(new Argument(CONFIG_SOURCE_DIRECTORY, "Specifies the source directory.", true, true));
				_helper.Arguments.Add(new Argument(CONFIG_DESTINATION_DIRECTORY, "Specifies the destination directory.", true, true));
				_helper.Arguments.Add(new Argument(CONFIG_CREATE_DIR_IF_NOT_EXIST, "Specifies the destination directory.", false, true) { DefaultValue = "true" });
				_helper.Arguments.Add(new Argument(CONFIG_USE_COMPRESSION, "Specifies whether the backup should be compressed.", false, true) { DefaultValue = "true" });
				_helper.Arguments.Add(new Argument(CONFIG_COPIES_TO_KEEP, "The number of copies that should be kept.  Using a zero (0) will keep all copies.", false, true) { DefaultValue = "1" });
				_helper.Arguments.Add(new Argument(CONFIG_ON_START_DELETE_COPIES, string.Format("Specifies whether extra copies of backup files and subdirectories (number specified using the -{0} parameter) should be deleted prior to starting the backup.  When set to false, extra copies are deleted after the backup has been created.  This requires more space on disk because the new backup needs to take up space before the old backup(s) are removed.  Setting this value to true removes the old backups before creating the new backup, so less space is needed.  However, if an error occurs it will result in fewer good backups on disk.", CONFIG_COPIES_TO_KEEP), false, false) { DefaultValue = "true" });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_SEND_TEST, "Sends a test email to verify that email is configured properly.", false, false) { DefaultValue = "false", EnforceChecks = false, RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_HOST, "The SMTP host server for sending emails.", false, true) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_PORT, CONFIG_EMAIL_TO_ADDRESS, CONFIG_EMAIL_TO_NAME, CONFIG_EMAIL_FROM_ADDRESS, CONFIG_EMAIL_FROM_NAME } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_PORT, "The SMTP port for sending emails.", false, true) { DefaultValue = "25", RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_SSL, "Whether or not the SMTP connection should use SSL.", false, false) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_FROM_ADDRESS, "The reply-to address used for sending emails.", false, true) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_FROM_NAME, "The reply-to name used for sending emails.", false, true) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_TO_ADDRESS, "The address to send mail to when sending emails.", false, true) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_EMAIL_TO_NAME, "The name to send mail to when sending emails.", false, true) { RequiredIfSetList = new Collection<string>() { CONFIG_EMAIL_HOST } });
				_helper.Arguments.Add(new Argument(CONFIG_RUN_IF_OLDER_THAN, "Specifies the number of days the newest file's last modified date should be before this script should run.", false, true));
				_helper.Arguments.Add(new Argument(CONFIG_REQUIRED_DRIVE_LETTER, "Specifies the drive letter (ex. 'C' for C:\\) of a drive that must exist for this script to run.", false, true));
				_helper.Arguments.Add(new Argument(CONFIG_REQUIRED_DRIVE_NAME, string.Format("Specifies the drive name (ex. 'MyDrive') of a drive that must exist for this script to run. When using this option, '{{DRIVE}}:\\' must be used in the beginning of the -{0} value and will be replaced with the drive letter found with this name.", CONFIG_DESTINATION_DIRECTORY), false, true));
				_helper.Arguments.Add(new Argument(CONFIG_SOURCE_COPY_NEWEST, string.Format("Instructs the script to copy only the newest file from the -{0} directory.", CONFIG_SOURCE_DIRECTORY), false, false));
				
				_helper.UsageTitle = "BackupUtility.exe - Backs up files.";
				if (!_helper.ParseArguments(args)) { return; }

				// Setup global variables
				_debugMode = _helper.GetBoolArgument(CONFIG_DEBUG);
				string jobName = _helper.GetStringArgument(CONFIG_NAME);
				string destinationDirectoryPath = _helper.GetStringArgument(CONFIG_DESTINATION_DIRECTORY);

				// Check to see if we're trying to test the email settings.  Do this first because the script aborts if this is the case.
				if (!string.IsNullOrEmpty(_helper.GetStringArgument(CONFIG_EMAIL_HOST))) {
					_emailClient = new SmtpClient(_helper.GetStringArgument(CONFIG_EMAIL_HOST), _helper.GetIntArgument(CONFIG_EMAIL_PORT));
					_emailClient.EnableSsl = _helper.GetBoolArgument(CONFIG_EMAIL_SSL);
					MailAddress fromAddress = new MailAddress(_helper.GetStringArgument(CONFIG_EMAIL_FROM_ADDRESS), _helper.GetStringArgument(CONFIG_EMAIL_FROM_NAME));
					MailAddress toAddress = new MailAddress(_helper.GetStringArgument(CONFIG_EMAIL_TO_ADDRESS), _helper.GetStringArgument(CONFIG_EMAIL_TO_NAME));
					_emailMessage = new MailMessage(fromAddress, toAddress);

					if (_helper.GetBoolArgument(CONFIG_EMAIL_SEND_TEST)) {
						_emailMessage.Body = "This is a test message from the BackupUtility to confirm that your email is working properly.";
						_emailMessage.Subject = "Test Message From BackupUtility";
						_emailClient.Send(_emailMessage);
						return;
					}
				}

				// Check for required drives.  If a drive isn't connected, we have to quit early, and we can't do this after tracing
				// because the destination is in a folder of a drive that doesn't exist.
				if (!string.IsNullOrEmpty(_helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_LETTER))) {
					DriveInfo drive = new DriveInfo(_helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_LETTER));
					if (!drive.IsReady) {
						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Drive '{0}' is not connected or it is not ready for use, aborting script.", _helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_LETTER));
						return;
					}
				} else if (!string.IsNullOrEmpty(_helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_NAME))) {
					if (!destinationDirectoryPath.StartsWith("{DRIVE}:\\")) {
						throw new ConsoleException("-{0} must start with '{{DRIVE}}:\\' when using -{1}.  See help content for more details.", CONFIG_DESTINATION_DIRECTORY, CONFIG_REQUIRED_DRIVE_NAME);
					}

					// Searching for the drive by name.
					DriveInfo drive = null;
					foreach (DriveInfo availableDrive in DriveInfo.GetDrives()) {
						if (availableDrive.IsReady && availableDrive.VolumeLabel == _helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_NAME)) {
							_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Found drive '{0}' at drive letter '{1}'.", availableDrive.VolumeLabel, availableDrive.Name);
							drive = availableDrive;
							destinationDirectoryPath = destinationDirectoryPath.Replace("{DRIVE}:\\", availableDrive.Name);
						}
					}

					if (drive == null) {
						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Drive '{0}' is not connected or it is not ready for use, aborting script.", _helper.GetStringArgument(CONFIG_REQUIRED_DRIVE_NAME));
						return;
					}
				}

				// Setup the destination directory.
				DirectoryInfo destinationDirectory = new DirectoryInfo(destinationDirectoryPath);
				if (!destinationDirectory.Exists) {
					if (_helper.GetBoolArgument(CONFIG_CREATE_DIR_IF_NOT_EXIST)) {
						_helper.LogFormat(ConsoleHelper.WriteMode.Console, "Creating directory: {0}", destinationDirectoryPath);
						if (!_debugMode) {
							destinationDirectory = Directory.CreateDirectory(destinationDirectoryPath);
						}
					} else {
						throw new ConsoleException("Destination directory '{0}' does not exist.", destinationDirectoryPath);
					}
				}

				// ******************************************************************************************************* //
				//                                          TRACING ENABLED BELOW                                          //
				// ******************************************************************************************************* //
				CommonLibrary.Console.Tracing.Trace.Enabled = true;
				CommonLibrary.Console.Tracing.Trace.Overwrite = true;
				CommonLibrary.Console.Tracing.Trace.FullFilePath = string.Format("{0}\\log.txt", destinationDirectory.FullName);
				_helper.LogFormat(ConsoleHelper.WriteMode.Console, "Log file created at {0}.", CommonLibrary.Console.Tracing.Trace.FullFilePath);

				// Check to see if we need to wait for another task to complete.
				if (!string.IsNullOrEmpty(_helper.GetStringArgument(CONFIG_WAIT_FOR_TASKS))) {
					string value = _helper.GetStringArgument(CONFIG_WAIT_FOR_TASKS);
					string[] preTasks = value.Split(',');
					ScheduledTaskHelper.WaitForRunningTask(preTasks, _helper.GetIntArgument(CONFIG_WAIT_SECONDS), _helper);
				}

				// Ensure that WinRAR is setup correctly
				if (_helper.GetBoolArgument(CONFIG_USE_COMPRESSION) && string.IsNullOrEmpty(ConfigurationManager.AppSettings[WinRarPathConfigKey])) {
					throw new ConsoleException("Compression was turned on using -{0} but the '{1}' configuration that points to the WinRAR executable is missing or empty.  Please edit the configuration and specify the path to the WinRAR executable.", CONFIG_USE_COMPRESSION, WinRarPathConfigKey);
				}

				// Check to see if we need to run only if the files in the detination directory are older than a certain age.
				int runIfOlderThan = _helper.GetIntArgument(CONFIG_RUN_IF_OLDER_THAN);
				if (runIfOlderThan > 0) {
					IOrderedEnumerable<FileInfo> files = destinationDirectory.GetFiles("*.rar").OrderByDescending(a => a.CreationTime);
					if (files.Count() > 0) {
						FileInfo file = files.First();
						bool continueScript = true;
						string comparisonText = string.Empty;
						int daysAgoLastWrite = DateTime.Now.Subtract(file.LastWriteTime).Days;
						
						if (daysAgoLastWrite < runIfOlderThan) {
							comparisonText = "newer than";
							continueScript = false;
						} else if (daysAgoLastWrite == runIfOlderThan) {
							comparisonText = "equal to";
						} else {
							comparisonText = "older than";
						}

						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Newest file '{0}' is {1} day(s) old, which is {2} the {3} day age requirement, {4} script.", file.FullName, daysAgoLastWrite.ToString(), comparisonText, runIfOlderThan, (continueScript) ? "continuing" : "aborting");
						if (!continueScript) {
							return;
						}
					} else {
						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "There are no files in the destination directory of {0}, so -{1} check has nothing to compare to.  Continuing script.", destinationDirectory.FullName, CONFIG_RUN_IF_OLDER_THAN);
					}
				}

				// Configure the source directory
				DirectoryInfo sourceDirectory = new DirectoryInfo(_helper.GetStringArgument(CONFIG_SOURCE_DIRECTORY));
				if (!sourceDirectory.Exists) {
					throw new ConsoleException("Source directory '{0}' does not exist.", _helper.GetStringArgument(CONFIG_SOURCE_DIRECTORY));
				}

				// Check to see if we should pre-delete the copies files
				bool copiesDeleted = false;
				if (_helper.GetBoolArgument(CONFIG_ON_START_DELETE_COPIES)) {
					int copies = _helper.GetIntArgument(CONFIG_COPIES_TO_KEEP);
					copies = copies - 1; // Decrement the count by one because the one added will be the new backup.
					string searchFileName = jobName;
					if (_helper.GetBoolArgument(CONFIG_SOURCE_COPY_NEWEST)) {
						_helper.LogFormat(ConsoleHelper.WriteMode.Log, "'-{0}' was set, so in source copy mode.  Overriding the file search pattern for deleting files.", CONFIG_SOURCE_COPY_NEWEST);
						FileInfo[] files = sourceDirectory.GetFiles("*.rar");
						if (files.Length > 0) {
							searchFileName = files[0].Name.Substring(0, files[0].Name.IndexOf(" - "));
							_helper.LogFormat(ConsoleHelper.WriteMode.Log, "Basing search pattern off of file '{0}' from the source directory.  Search pattern name is now '{1}'.", files[0].Name, searchFileName);
						} else {
							_helper.LogFormat(ConsoleHelper.WriteMode.Log, "No files were found in the source directory {0}.", sourceDirectory.FullName);
						}
					}

					Program.ClearFiles(copies, destinationDirectory, searchFileName, _debugMode);
					copiesDeleted = true;
				}

				// Configure the destination directory
				string archiveName = string.Format("{0} - {1}", jobName, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
				string archiveNameFull = string.Format("{0}\\{1}", destinationDirectory.FullName, archiveName);

				// Begin the backup
				if (_helper.GetBoolArgument(CONFIG_SOURCE_COPY_NEWEST)) {
					// This is in archiving mode, meaning that we're not backing up files, but instead are copying an existing backup to
					// further archive the file.
					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Copying newest *.rar file ...");
					IOrderedEnumerable<FileInfo> files = sourceDirectory.GetFiles("*.rar").OrderByDescending(a => a.CreationTime);
					if (files.Count() > 0) {
						FileInfo file = files.First();
						string destinationFile = string.Format("{0}\\{1}", destinationDirectory.FullName, file.Name);
						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Beginning copy of {0} to {1}", file.FullName, destinationFile);
						if (!_debugMode) {
							file.CopyTo(destinationFile);
						}
					} else {
						throw new ConsoleException("Could not find any *.rar files in source directory '{0}'.", sourceDirectory.ToString());
					}
				} else {
					// This is a true backup, so copy all the source files into a directory that will later be compressed.
					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Beginning backup of {0} to {1}", sourceDirectory.FullName, archiveNameFull);
					if (!_debugMode) {
						_helper.ExecuteCommand("ROBOCOPY", string.Format("\"{0}\" \"{1}\" /A-:R /MIR /R:2", sourceDirectory.FullName, archiveNameFull));
					}
				}
				_helper.LogFormat(ConsoleHelper.WriteMode.Log, "Copy complete.");

				// Compress the backup
				if (_helper.GetBoolArgument(CONFIG_USE_COMPRESSION)) {
					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Compressing archive ...");

					if (!_debugMode) {
						_helper.ExecuteCommand(ConfigurationManager.AppSettings[WinRarPathConfigKey], string.Format("a -r -k -m4 -os -ilog{3} -ep1 \"{0}\\{1}\" \"{2}\"", destinationDirectory.FullName, archiveName, archiveNameFull, ConfigurationManager.AppSettings["WinRARErrorLogFile"]));
					}

					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Compressing complete.");

					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Deleting {0} ...", archiveNameFull);
					try {
						if (!_debugMode) {
							Directory.Delete(archiveNameFull, true);
						}
					} catch (Exception ex) {
						// Skip over issues right now ... we'll come back through later and clean up any messes due to long filenames.
						_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Error encountered deleting folder, will retry later: {0}", ex.Message);
					}

					if (!_debugMode) {
						if (Directory.Exists(archiveNameFull) && Directory.GetDirectories(archiveNameFull).Length > 0) {
							Program.RecursiveRename(archiveNameFull);
							Directory.Delete(archiveNameFull, true);
						}
					}
					_helper.LogFormat(ConsoleHelper.WriteMode.Both, "Delete complete.");
				}

				if (!copiesDeleted) {
					// Do post-processing cleanup of directories
					Program.ClearFiles(_helper.GetIntArgument(CONFIG_COPIES_TO_KEEP), destinationDirectory, jobName, _debugMode);
				}

				CommonLibrary.Console.Tracing.Trace.WriteLine("Script completed.");
				CommonLibrary.Console.Tracing.Trace.WriteLine();
			} catch (ConsoleException ex) {
				_helper.LogFormat(ConsoleHelper.LogType.Error, "Handled Error: {0}", ex.Message);
			} catch (Exception ex) {
				_helper.LogFormat(ConsoleHelper.LogType.Error, "Unhandled Error: {0}.  Stack: {1}", ex.Message, ex.StackTrace);
				if (_emailClient != null && _emailMessage != null) {
					string jobName = _helper.GetStringArgument(CONFIG_NAME);
					if (!string.IsNullOrEmpty(jobName)) {
						_emailMessage.Subject = string.Format("Exception in BackupUtility, Job: {0}", jobName);
						_emailMessage.Body = string.Format("An exception occurred while running the BackupUtility during the '{0}' job.", jobName);
					} else {
						_emailMessage.Subject = "Exception in BackupUtility";
						_emailMessage.Body = "An exception occurred while running the BackupUtility.";
					}
					_emailMessage.Body = string.Format("{0}\n\nMessage: {1}\n\nStack: {2}", _emailMessage.Body, ex.Message, ex.StackTrace);
					_emailClient.Send(_emailMessage);
				}
			} finally {
				if (!CommonLibrary.Console.Tracing.Trace.Enabled) {
					CommonLibrary.Console.Tracing.Trace.Enabled = true;
					CommonLibrary.Console.Tracing.Trace.Overwrite = true;
					CommonLibrary.Console.Tracing.Trace.FullFilePath = ConfigurationManager.AppSettings["TempLogFile"];
					CommonLibrary.Console.Tracing.Trace.Flush();
				}
			}
		}

		private static void RecursiveRename(string currentDirectoryPath) {
			int dirCount = 0;
			int fileCount = 0;

			foreach (string subFilePath in Directory.GetFiles(currentDirectoryPath)) {
				string extension = subFilePath.Substring(subFilePath.LastIndexOf('.')+1);
				string newName = string.Format("{0}\\f{1}.{2}", currentDirectoryPath, fileCount.ToString(), extension);
				File.Move(subFilePath, newName);
				fileCount++;
			}

			foreach (string subDirectoryPath in Directory.GetDirectories(currentDirectoryPath)) {
				string newName = string.Format("{0}\\d{1}", currentDirectoryPath, dirCount.ToString());
				Directory.Move(subDirectoryPath, newName);
				dirCount++;
				Program.RecursiveRename(newName);
			}
		}

		private static void ProcessFileObjects(FileSystemInfo[] fileSystemObjects, Dictionary<FileSystemInfo, DateTime> fileObjects) {
			foreach (FileSystemInfo fsObject in fileSystemObjects) {
				fileObjects.Add(fsObject, fsObject.CreationTime);
			}
		}

		private static void ClearFiles(int copiesToKeep, DirectoryInfo directory, string fileSearchBeginPattern, bool debugMode) {
			if (copiesToKeep >= 0 && directory.Exists) {
				fileSearchBeginPattern = string.Concat(fileSearchBeginPattern, "*");
				_helper.LogFormat("Searching for files and directories to clear using '{0}', keeping {1} files.", fileSearchBeginPattern, copiesToKeep);
				DirectoryInfo[] subDirectories = directory.GetDirectories(fileSearchBeginPattern, SearchOption.TopDirectoryOnly);
				FileInfo[] subFiles = directory.GetFiles(fileSearchBeginPattern, SearchOption.TopDirectoryOnly);

				Dictionary<FileSystemInfo, DateTime> fileObjects = new Dictionary<FileSystemInfo, DateTime>(); ;
				Program.ProcessFileObjects(subDirectories, fileObjects);
				Program.ProcessFileObjects(subFiles, fileObjects);
				IOrderedEnumerable<KeyValuePair<FileSystemInfo, DateTime>> sortedFileObjects = fileObjects.OrderByDescending(obj => obj.Value);
				int count = 1;
				foreach (KeyValuePair<FileSystemInfo, DateTime> fileObject in sortedFileObjects) {
					if (count > copiesToKeep) {
						string type = (fileObject.Key is FileInfo) ? "file" : "directory";
						_helper.LogFormat(ConsoleHelper.WriteMode.Log, "Deleting {0}: {1}", type, fileObject.Key.FullName);

						if (!debugMode) {
							if (fileObject.Key is DirectoryInfo) {
								DirectoryInfo directoryDelete = (DirectoryInfo)fileObject.Key;
								directoryDelete.Delete(true);
							} else {
								fileObject.Key.Delete();
							}
						}
					}
					count++;
				}
			}
		}
	}
}