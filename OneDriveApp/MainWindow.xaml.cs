using Microsoft.Graph;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace OneDriveApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string MsaClientId = "c1dff020-76af-4d00-b23a-dd30f761a164";
        public const string MsaReturnUrl = "urn:ietf:wg:oauth:2.0:oob";
        private enum ClientType
        {
            Consumer,
            Business
        }
        private GraphServiceClient graphClient { get; set; }
        private ClientType clientType { get; set; }
        private DriveItem CurrentFolder { get; set; }
        private DriveItem SelectedItem { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
            Upload.IsEnabled = false;
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

            MessageBox.Show(string.Format("OneDrive reported the following error: {0}", message));
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
                }
                else
                {
                    folder = await graphClient.Drive.Root.ItemWithPath("/" + path).Request().Expand(expandValue).GetAsync();
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
                LoadProperties(folder);
                if (folder.Folder != null && folder.Children != null && folder.Children.CurrentPage != null)
                {
                    LoadChildren(folder.Children.CurrentPage);
                }
            }
        }
        private void LoadProperties(DriveItem item)
        {
            SelectedItem = item;
        }
        private async void Download(object sender, RoutedEventArgs e)
        {
            var m = lvUsers.SelectedIndex;
            var listOfDriveItems = (List<DriveItem>)(lvUsers.ItemsSource);
            var driveItem = listOfDriveItems[m];
            LoadProperties(driveItem);
            if (driveItem != null)
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
                MessageBox.Show("Error uploading file: " + ex.Message);
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
                        MessageBox.Show("Uploaded with ID: " + uploadedItem.Id);
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
    }
}
