using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FastTextDelete
{
    public partial class MainViewModel : ObservableObject
    {
        public MainViewModel()
        {
            SelectDirectoryCommand = new RelayCommand(SelectDirectoryMethod, CanSelectDirectory);
            DeleteDuplicatesCommand = new RelayCommand(DeleteDuplicatesMethod, CanDeleteDuplicates);
            DeleteFilesWithNameCommand = new RelayCommand(DeleteFilesWithNameMethod, CanDeleteFilesWithName);
            DeleteFilesWithContentCommand = new RelayCommand(DeleteFilesWithContentMethod, CanDeleteFilesWithContent);
            SaveCommand = new RelayCommand(SaveMethod, CanSave);
            DeleteCommand = new RelayCommand(DeleteMethod, CanDelete);

            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);

            if (!Directory.Exists(DeletePath))
                Directory.CreateDirectory(DeletePath);
        }

        #region Static Configuration

        static string SavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Organized");
        static string DeletePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Trash");
        static string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log.txt");

        #region Affects Files with Following Extensions
        // These are only used for deleting/saving bound commmands used by the MainWindow's InputBinding.
        // DeleteFilesWithName and DeleteFilesWithContent only process text files.

        static string[] Extensions = { "*.json", "*.txt", "*.html", "*.csv" };
        #endregion

        #endregion

        #region Observable Properties
        [ObservableProperty]
        public string? targetDirectory;

        [ObservableProperty]
        public string? deleteFilenameContainingStrings;

        [ObservableProperty]
        public string? deleteContentContainingStrings;

        [ObservableProperty]
        public ObservableCollection<FileInfo>? textFiles;
        #endregion

        #region Commands

        public IRelayCommand SelectDirectoryCommand { get; }
        public IRelayCommand DeleteDuplicatesCommand { get; }
        public IRelayCommand DeleteFilesWithNameCommand { get; }
        public IRelayCommand DeleteFilesWithContentCommand { get; }
        public IRelayCommand SaveCommand { get; }
        public IRelayCommand DeleteCommand { get; }

        #region Methods

        private void SelectDirectoryMethod()
        {
            var folderDialog = new OpenFolderDialog
            {
                ShowHiddenItems = true
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                TargetDirectory = folderName;
                NotifyCanExecute();
                UpdateTextFiles();
            }
        }

        public void DeleteDuplicatesMethod()
        {
            if (TargetDirectory != null)
            {
                Helpers.DeleteDuplicateFiles(TargetDirectory);
                UpdateTextFiles();
            }
            else
                MessageBox.Show("Select folder first.");
        }

        public void DeleteFilesWithNameMethod()
        {
            if (TargetDirectory != null && DeleteFilenameContainingStrings != null)
            {
                foreach (var redFlag in DeleteFilenameContainingStrings.Replace(" ", "").Split(','))
                {
                    Helpers.Log($"Auto Deleting Files with Name: `{redFlag}`", LogFile);
                    Helpers.DeleteFilesWithNameContainingString(TargetDirectory, redFlag, DeletePath);
                    UpdateTextFiles();
                }
            }
            else
                MessageBox.Show("Select folder or set filenames to delete first.");
        }
       
        public void DeleteFilesWithContentMethod()
        {
            if (TargetDirectory != null && DeleteContentContainingStrings != null)
            {
                foreach (var redFlag in DeleteContentContainingStrings.Replace(" ", "").Split(','))
                {
                    Helpers.Log($"Auto Deleting Files with Content: `{redFlag}`", LogFile);
                    Helpers.DeleteFilesContainingString(TargetDirectory, redFlag, DeletePath);
                    UpdateTextFiles();
                }
            }
            else
                MessageBox.Show("Select folder or set filenames to delete first.");
        }

        public void SaveMethod()
        {
            if (TextFiles.Count > 0)
            {
                Helpers.MoveFileToFolder(TextFiles?[0].FullName, SavePath);
                Helpers.Log($"Saved: {TextFiles?[0].FullName}", LogFile);
                UpdateTextFileOptimal();
            }
        }

        public void DeleteMethod()
        {
            if (TextFiles.Count > 0)
            {
                Helpers.MoveFileToFolder(TextFiles?[0].FullName, DeletePath);
                Helpers.Log($"Deleted: {TextFiles?[0].FullName}", LogFile);
                UpdateTextFileOptimal();
            }
        }

        #endregion

        #region Can Above Methods Execute?

        private void NotifyCanExecute()
        {
            SelectDirectoryCommand.NotifyCanExecuteChanged();
            DeleteDuplicatesCommand.NotifyCanExecuteChanged();
            DeleteFilesWithNameCommand.NotifyCanExecuteChanged();
            DeleteFilesWithContentCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        public bool CanSelectDirectory()
        {
            // Could be problematic if directory changed while processing
            return true;
        }

        public bool CanDeleteDuplicates()
        {
            return TargetDirectory != null;
        }

        public bool CanDeleteFilesWithName()
        {
            return TargetDirectory != null;
        }

        public bool CanDeleteFilesWithContent()
        {
            return TargetDirectory != null;
        }

        public bool CanSave()
        {
            return TargetDirectory != null;
        }

        public bool CanDelete()
        {
            return TargetDirectory != null;
        }

        #endregion

        #endregion

        #region Update Text Files Collection Functions
        private void UpdateTextFileOptimal()
        {
            if (TextFiles != null && TextFiles.Count != 0)
            {
                if (!File.Exists(TextFiles[0].FullName))
                    TextFiles.RemoveAt(0);
            }
        }

        private void UpdateTextFiles()
        {
            if (Directory.Exists(TargetDirectory))
            {
                var bufferCollection = new ObservableCollection<FileInfo>();

                var extensions = Extensions;
                var files = extensions.SelectMany(ext => Directory.GetFiles(TargetDirectory, ext, System.IO.SearchOption.AllDirectories))
                                      .Select(path => new FileInfo(path));

                foreach (var file in files)
                {
                    bufferCollection.Add(file);
                }

                TextFiles = bufferCollection;
            }

            else
            {
                TextFiles?.Clear();
            }
        }
        #endregion
    }

    public static class Helpers
    {
        public static void Log(string Message, string TextFile)
        {
            File.AppendAllText(TextFile, Message);
        }

        public static void MoveFileToTrash(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                FileSystem.DeleteFile(
                    filePath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin
                );
            }
            else
            {
                Debug.WriteLine("File not found: " + filePath);
            }
        }

        public static void MoveFileToFolder(string sourceFilePath, string destinationFolderPath)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("The source file does not exist.", sourceFilePath);
            }

            if (!Directory.Exists(destinationFolderPath))
            {
                Directory.CreateDirectory(destinationFolderPath);
            }

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(destinationFolderPath, fileName);

            if (File.Exists(destinationPath))
            {
                destinationPath = GetUniqueFileName(destinationFolderPath, fileName);
            }

            File.Move(sourceFilePath, destinationPath);
        }

        private static string GetUniqueFileName(string folderPath, string fileName)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int counter = 1;

            Regex regex = new Regex(@"\((\d+)\)$");

            Match match = regex.Match(nameWithoutExtension);
            if (match.Success)
            {
                nameWithoutExtension = nameWithoutExtension.Substring(0, match.Index).Trim();
            }

            string newFileName;
            string newFilePath;

            do
            {
                counter++;
                newFileName = $"{nameWithoutExtension} ({counter}){extension}";
                newFilePath = Path.Combine(folderPath, newFileName);
            } while (File.Exists(newFilePath));

            return newFilePath;
        }

        public static void DeleteFilesWithNameContainingString(string directoryPath, string stringToCheck, string DeletePath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string[] textFiles = Directory.GetFiles(directoryPath, "*.txt", System.IO.SearchOption.AllDirectories);

            foreach (string file in textFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(file);

                    if (fileName.Contains(stringToCheck))
                    {
                        MoveFileToFolder(file, DeletePath);
                        Debug.WriteLine($"Deleted: {file}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing file {file}: {ex.Message}");
                }
            }
        }

        public static void DeleteFilesContainingString(string directoryPath, string searchString, string DeletePath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
            }

            if (string.IsNullOrWhiteSpace(searchString))
            {
                throw new ArgumentException("Search string cannot be null or empty.", nameof(searchString));
            }

            Debug.WriteLine($"Deleting Search String: {searchString}");

            try
            {
                string[] textFiles = Directory.GetFiles(directoryPath, "*.txt", System.IO.SearchOption.AllDirectories);

                foreach (var file in textFiles)
                {
                    string fileContent = File.ReadAllText(file, System.Text.Encoding.UTF8);
                    if (fileContent.Contains(searchString))
                    {
                        MoveFileToFolder(file, DeletePath);
                    }
                }
            }

            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        public static void DeleteDuplicateFiles(string folderPath)
        {
            try
            {
                var fileHashes = new Dictionary<string, List<string>>();

                var files = Directory.GetFiles(folderPath, "*.txt", System.IO.SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    string fileHash = ComputeFileHash(file);

                    if (!fileHashes.ContainsKey(fileHash))
                    {
                        fileHashes[fileHash] = new List<string>();
                    }
                    fileHashes[fileHash].Add(file);
                }

                foreach (var hashGroup in fileHashes)
                {
                    var duplicateFiles = hashGroup.Value;

                    for (int i = 1; i < duplicateFiles.Count; i++)
                    {
                        MoveFileToTrash(duplicateFiles[i]);
                        Debug.WriteLine($"Deleted duplicate: {duplicateFiles[i]}");
                    }
                }

                Debug.WriteLine("Duplicate file removal completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        static string ComputeFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = md5.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }

}
