using Microsoft.Graph;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Controls;

namespace OneDriveApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum ClientType
        {
            Consumer,
            Business
        }
        private GraphServiceClient graphClient { get; set; }
        private ClientType clientType { get; set; }
        private DriveItem CurrentFolder { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
            Upload.IsEnabled = false;
            Back.IsEnabled = false;
            Back.Visibility = Visibility.Hidden;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                graphClient = AuthenticationHelper.GetAuthenticatedClient();
            }
            catch (ServiceException exception)
            {
                PresentServiceException(exception);
            }
            try
            {
                await LoadFolderFromPath();
                Upload.IsEnabled = true;
            }
            catch (ServiceException exception)
            {
                PresentServiceException(exception);
                graphClient = null;
            }
        }

        private static void PresentServiceException(Exception exception)
        {
            string message = null;
            var oneDriveException = exception as ServiceException;
            if (oneDriveException == null)
            {
                message = exception.Message;
            }
            else
            {
                message = string.Format("{0}{1}", Environment.NewLine, oneDriveException.ToString());
            }

            MessageBox.Show(string.Format("OneDrive reported the following error: {0}", message), "Message");
        }
        private async Task LoadFolderFromPath(string path = null)
        {
            if (null == graphClient) return;
            LoadChildren(new DriveItem[0]);
            try
            {
                DriveItem folder;
                var expandValue = clientType == ClientType.Consumer ? "thumbnails,children($expand=thumbnails)" : "thumbnails,children";
                if (path == null)
                {
                    folder = await graphClient.Drive.Root.Request().Expand(expandValue).GetAsync();
                    Back.IsEnabled = false;
                    Back.Visibility = Visibility.Hidden;
                }
                else
                {
                    folder = await graphClient.Drive.Root.ItemWithPath("/" + path).Request().Expand(expandValue).GetAsync();
                    Back.IsEnabled = true;
                    Back.Visibility = Visibility.Visible;
                }
                ProcessFolder(folder);
            }
            catch (Exception exception)
            {
                PresentServiceException(exception);
            }
        }
        private void LoadChildren(IList<DriveItem> items)
        {
            lvUsers.ItemsSource = items;
        }
        private void ProcessFolder(DriveItem folder)
        {
            if (folder != null)
            {
                CurrentFolder = folder;
                if (folder.Folder != null && folder.Children != null && folder.Children.CurrentPage != null)
                {
                    LoadChildren(folder.Children.CurrentPage);
                }
            }
        }
        private async void Download(object sender, RoutedEventArgs e)
        {
            var m = lvUsers.SelectedIndex;
            var listOfDriveItems = (IList<DriveItem>)(lvUsers.ItemsSource);
            if (m != -1)
            {
                var driveItem = listOfDriveItems[m];
                if (driveItem != null && driveItem.Folder == null)
                {
                    var dialog = new SaveFileDialog();
                    dialog.FileName = driveItem.Name;
                    dialog.Filter = "All Files (*.*)|*.*";
                    var result = dialog.ShowDialog();
                    if (result.HasValue && result.Value != true)
                        return;
                    using (var stream = await graphClient.Drive.Items[driveItem.Id].Content.Request().GetAsync())
                    using (var outputStream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create))
                    {
                        await stream.CopyToAsync(outputStream);
                        MessageBox.Show(driveItem.Name + " was saved.", "Message");
                    }
                }
                if (driveItem.Folder != null)
                {
                    var path = (driveItem.ParentReference.Path + "/" + driveItem.Name).Remove(0, 12);
                    await LoadFolderFromPath(path);
                }
            }
        }
        private System.IO.Stream GetFileStreamForUpload(string targetFolderName, out string originalFilename)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Upload to " + targetFolderName;
            dialog.Filter = "All Files (*.*)|*.*";
            dialog.CheckFileExists = true;
            var response = dialog.ShowDialog();
            if (response.HasValue && response.Value != true)
            {
                originalFilename = null;
                return null;
            }
            try
            {
                originalFilename = System.IO.Path.GetFileName(dialog.FileName);
                return new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error uploading file: " + ex.Message, "Message");
                originalFilename = null;
                return null;
            }
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            var targetFolder = CurrentFolder;
            string filename;
            using (var stream = GetFileStreamForUpload(targetFolder.Name, out filename))
            {
                if (stream != null)
                {
                    string folderPath = targetFolder.ParentReference.Path == null ? ""
                        : targetFolder.ParentReference.Path.Remove(0, 12) + "/" + Uri.EscapeUriString(targetFolder.Name);
                    var uploadPath = folderPath + "/" + Uri.EscapeUriString(System.IO.Path.GetFileName(filename));
                    try
                    {
                        var uploadedItem =
                            await
                                this.graphClient.Drive.Root.ItemWithPath(uploadPath).Content.Request().PutAsync<DriveItem>(stream);
                        MessageBox.Show(filename + " was uploaded", "Message");
                        var listOfDriveItems = (List<DriveItem>)(lvUsers.ItemsSource);
                        lvUsers.ItemsSource = null;
                        listOfDriveItems.Add(uploadedItem);
                        lvUsers.ItemsSource = listOfDriveItems;
                    }
                    catch (Exception exception)
                    {
                        PresentServiceException(exception);
                    }
                }
            }
        }
        private async void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder.ParentReference.Path == "/drive/root:")
            {
                await LoadFolderFromPath();
            }
            else
            {
                var path = (CurrentFolder.ParentReference.Path).Remove(0, 13);
                await LoadFolderFromPath(path);
            }
        }
    }
}
