using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HOWTOUSE
{
    public partial class DutyManualView : UserControl
    {
        private readonly ObservableCollection<DutyManualItem> allItems = new ObservableCollection<DutyManualItem>();
        private readonly ObservableCollection<DutyManualItem> filteredItems = new ObservableCollection<DutyManualItem>();
        private readonly ObservableCollection<ManualAttachment> editingAttachments = new ObservableCollection<ManualAttachment>();
        private readonly List<ManualCategory> categories = new List<ManualCategory>();
        private DutyManualItem selectedItem;
        private bool isNewItem;
        private long? selectedCategoryId;

        public DutyManualView()
        {
            InitializeComponent();
            DutyListBox.ItemsSource = filteredItems;
            AttachmentListBox.ItemsSource = editingAttachments;
            LoadManualCategories();
            LoadManualItems();
            ApplyFilter();
            SetEditorEnabled(false);
        }

        private void ApplyFilter()
        {
            string keyword = DutySearchTextBox?.Text?.Trim() ?? string.Empty;
            HashSet<long> visibleCategoryIds = GetSelectedCategoryAndDescendants();
            filteredItems.Clear();
            foreach (DutyManualItem item in allItems.Where(item =>
                (selectedCategoryId == null || visibleCategoryIds.Contains(item.CategoryId)) && item.Contains(keyword))
                .OrderBy(item => item.CategorySortKey).ThenBy(item => item.Title))
                filteredItems.Add(item);
        }

        private HashSet<long> GetSelectedCategoryAndDescendants()
        {
            if (selectedCategoryId == null) return new HashSet<long>();
            HashSet<long> result = new HashSet<long> { selectedCategoryId.Value };
            Queue<long> pending = new Queue<long>(); pending.Enqueue(selectedCategoryId.Value);
            while (pending.Count > 0)
            {
                long parentId = pending.Dequeue();
                foreach (ManualCategory child in categories.Where(category => category.ParentCategoryId == parentId))
                    if (result.Add(child.CategoryId)) pending.Enqueue(child.CategoryId);
            }
            return result;
        }

        private void DutySearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void CategoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (CategoryTreeView.SelectedItem is TreeViewItem node)
                selectedCategoryId = (node.Tag as ManualCategory)?.CategoryId;
            ApplyFilter();
        }

        private void AddManualCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            string folderName = ShowTextInputDialog("폴더 추가", "새 폴더 이름");
            if (string.IsNullOrWhiteSpace(folderName)) return;
            folderName = folderName.Trim();

            const string getNextSortOrder = @"SELECT COALESCE(MAX(SORT_ORD), 0) + 1
                FROM MODUWMNL_CATEGORY
                WHERE PARENT_CATEGORY_ID <=> @PARENT_CATEGORY_ID AND USE_YN = 'Y'";
            const string insertCategory = @"INSERT INTO MODUWMNL_CATEGORY
                (PARENT_CATEGORY_ID, CATEGORY_NM, SORT_ORD, USE_YN, FSR_DTM, FSR_STF_NO, LSH_DTM, LSH_STF_NO)
                VALUES (@PARENT_CATEGORY_ID, @CATEGORY_NM, @SORT_ORD, 'Y', NOW(), @STF_NO, NOW(), @STF_NO)";
            try
            {
                using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
                {
                    object parentCategoryId = selectedCategoryId.HasValue ? (object)selectedCategoryId.Value : DBNull.Value;
                    connection.Open();
                    int nextSortOrder;
                    using (MySqlCommand command = new MySqlCommand(getNextSortOrder, connection))
                    {
                        command.Parameters.AddWithValue("@PARENT_CATEGORY_ID", parentCategoryId);
                        nextSortOrder = Convert.ToInt32(command.ExecuteScalar());
                    }
                    using (MySqlCommand command = new MySqlCommand(insertCategory, connection))
                    {
                        command.Parameters.AddWithValue("@PARENT_CATEGORY_ID", parentCategoryId);
                        command.Parameters.AddWithValue("@CATEGORY_NM", folderName);
                        command.Parameters.AddWithValue("@SORT_ORD", nextSortOrder);
                        command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO);
                        command.ExecuteNonQuery(); selectedCategoryId = command.LastInsertedId;
                    }
                }
                LoadManualCategories(); ApplyFilter();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("폴더를 추가하지 못했습니다. " + ex.Message, "EZHOWTOUSE");
            }
        }

        private void CategoryTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject element = e.OriginalSource as DependencyObject;
            while (element != null && !(element is TreeViewItem)) element = VisualTreeHelper.GetParent(element);
            if (element is TreeViewItem folder) { folder.IsSelected = true; folder.Focus(); }
        }

        private void RenameManualCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ManualCategory category = categories.FirstOrDefault(item => item.CategoryId == selectedCategoryId);
            if (category == null) { MessageBox.Show("전체 폴더의 이름은 변경할 수 없습니다.", "EZHOWTOUSE"); return; }
            string name = ShowTextInputDialog("폴더 이름 변경", "새 폴더 이름");
            if (string.IsNullOrWhiteSpace(name)) return;
            const string query = @"UPDATE MODUWMNL_CATEGORY SET CATEGORY_NM = @CATEGORY_NM, LSH_DTM = NOW(), LSH_STF_NO = @STF_NO WHERE CATEGORY_ID = @CATEGORY_ID";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CATEGORY_NM", name.Trim()); command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO); command.Parameters.AddWithValue("@CATEGORY_ID", category.CategoryId);
                connection.Open(); command.ExecuteNonQuery();
            }
            LoadManualCategories(); LoadManualItems(); ApplyFilter();
        }

        private void DeleteManualCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            ManualCategory category = categories.FirstOrDefault(item => item.CategoryId == selectedCategoryId);
            if (category == null) { MessageBox.Show("전체 폴더는 삭제할 수 없습니다.", "EZHOWTOUSE"); return; }

            const string hasChildren = @"SELECT COUNT(*) FROM MODUWMNL_CATEGORY WHERE PARENT_CATEGORY_ID = @CATEGORY_ID AND USE_YN = 'Y'";
            const string hasManuals = @"SELECT COUNT(*) FROM MODUWMNL WHERE CATEGORY_ID = @CATEGORY_ID AND USE_YN = 'Y'";
            const string deleteCategory = @"UPDATE MODUWMNL_CATEGORY SET USE_YN = 'N', LSH_DTM = NOW(), LSH_STF_NO = @STF_NO WHERE CATEGORY_ID = @CATEGORY_ID";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
            {
                connection.Open();
                if (GetCategoryRelatedCount(connection, hasChildren, category.CategoryId) > 0)
                {
                    MessageBox.Show("하위 폴더가 있어 삭제할 수 없습니다. 하위 폴더를 먼저 정리해주세요.", "EZHOWTOUSE"); return;
                }
                if (GetCategoryRelatedCount(connection, hasManuals, category.CategoryId) > 0)
                {
                    MessageBox.Show("이 폴더에 매뉴얼이 있어 삭제할 수 없습니다. 매뉴얼을 다른 폴더로 옮기거나 삭제해주세요.", "EZHOWTOUSE"); return;
                }
                if (MessageBox.Show($"'{category.Name}' 폴더를 삭제할까요?", "EZHOWTOUSE", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                using (MySqlCommand command = new MySqlCommand(deleteCategory, connection))
                {
                    command.Parameters.AddWithValue("@CATEGORY_ID", category.CategoryId); command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO);
                    command.ExecuteNonQuery();
                }
            }
            selectedCategoryId = null; LoadManualCategories(); ApplyFilter();
        }

        private static int GetCategoryRelatedCount(MySqlConnection connection, string query, long categoryId)
        {
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CATEGORY_ID", categoryId);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void MoveManualCategoryUpButton_Click(object sender, RoutedEventArgs e) => MoveManualCategory(-1);
        private void MoveManualCategoryDownButton_Click(object sender, RoutedEventArgs e) => MoveManualCategory(1);

        private void MoveManualCategory(int direction)
        {
            ManualCategory category = categories.FirstOrDefault(item => item.CategoryId == selectedCategoryId);
            if (category == null) return;
            List<ManualCategory> siblings = categories.Where(item => item.ParentCategoryId == category.ParentCategoryId).OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToList();
            int index = siblings.FindIndex(item => item.CategoryId == category.CategoryId); int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= siblings.Count) return;
            ManualCategory target = siblings[targetIndex];
            const string query = @"UPDATE MODUWMNL_CATEGORY SET SORT_ORD = CASE CATEGORY_ID WHEN @CATEGORY_ID THEN @TARGET_SORT WHEN @TARGET_ID THEN @CURRENT_SORT END, LSH_DTM = NOW(), LSH_STF_NO = @STF_NO WHERE CATEGORY_ID IN (@CATEGORY_ID, @TARGET_ID)";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CATEGORY_ID", category.CategoryId); command.Parameters.AddWithValue("@TARGET_ID", target.CategoryId); command.Parameters.AddWithValue("@CURRENT_SORT", category.SortOrder); command.Parameters.AddWithValue("@TARGET_SORT", target.SortOrder); command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO);
                connection.Open(); command.ExecuteNonQuery();
            }
            LoadManualCategories(); LoadManualItems(); ApplyFilter();
        }

        private void DutyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DutyListBox.SelectedItem is DutyManualItem item) LoadItem(item);
        }

        private void NewManualPostButton_Click(object sender, RoutedEventArgs e)
        {
            isNewItem = true; selectedItem = null; editingAttachments.Clear(); SetEditorEnabled(true);
            ManualEditorTitleTextBlock.Text = "새 매뉴얼"; ManualTitleInput.Text = string.Empty; ManualDetailInput.Text = string.Empty; ManualQueryInput.Text = string.Empty;
            ManualCategoryComboBox.SelectedItem = categories.FirstOrDefault(); ManualModifiedTextBlock.Text = string.Empty; DutyListBox.SelectedItem = null; ManualTitleInput.Focus();
        }

        private void LoadItem(DutyManualItem item)
        {
            isNewItem = false; selectedItem = item; SetEditorEnabled(true);
            ManualEditorTitleTextBlock.Text = item.Title; SelectCategory(item.CategoryId); ManualTitleInput.Text = item.Title; ManualDetailInput.Text = item.Detail; ManualQueryInput.Text = item.Query;
            editingAttachments.Clear(); foreach (ManualAttachment attachment in LoadManualAttachments(item.ManualId)) editingAttachments.Add(attachment);
            ManualModifiedTextBlock.Text = item.LastModifiedLabel;
        }

        private void SetEditorEnabled(bool enabled)
        {
            ManualCategoryComboBox.IsEnabled = enabled; ManualTitleInput.IsReadOnly = !enabled; ManualDetailInput.IsReadOnly = !enabled; ManualQueryInput.IsReadOnly = !enabled;
            AddAttachmentButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed; SaveManualButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            NewManualPostButton.Visibility = Visibility.Visible;
        }

        private void SaveManualButton_Click(object sender, RoutedEventArgs e)
        {
            string title = ManualTitleInput?.Text?.Trim() ?? string.Empty;
            string detail = ManualDetailInput?.Text?.Trim() ?? string.Empty;
            ManualCategory category = ManualCategoryComboBox.SelectedItem as ManualCategory;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(detail) || category == null) { MessageBox.Show("카테고리, 제목, 업무 안내를 입력해주세요.", "EZHOWTOUSE"); return; }
            long manualId = isNewItem || selectedItem == null
                ? InsertManual(category.CategoryId, title, detail, ManualQueryInput?.Text?.Trim() ?? string.Empty)
                : UpdateManual(selectedItem.ManualId, category.CategoryId, title, detail, ManualQueryInput?.Text?.Trim() ?? string.Empty);
            SaveManualAttachments(manualId); LoadManualItems(); selectedItem = allItems.FirstOrDefault(item => item.ManualId == manualId);
            isNewItem = false; ApplyFilter(); DutyListBox.SelectedItem = selectedItem; if (selectedItem != null) LoadItem(selectedItem);
        }

        private void AddAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            string sharePath = AppSettings.Current.Attachment.SharePath;
            OpenFileDialog dialog = new OpenFileDialog { Filter = "지원 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.ppt;*.pptx|사진|*.jpg;*.jpeg;*.png;*.bmp;*.gif|PowerPoint|*.ppt;*.pptx", Multiselect = true, InitialDirectory = Directory.Exists(sharePath) ? sharePath : string.Empty };
            if (dialog.ShowDialog() != true) return;
            foreach (string path in dialog.FileNames)
            {
                string destPath = Path.Combine(sharePath, Guid.NewGuid() + Path.GetExtension(path));
                File.Copy(path, destPath, true); editingAttachments.Add(new ManualAttachment(destPath));
            }
        }

        private void RemoveAttachmentButton_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is ManualAttachment attachment) editingAttachments.Remove(attachment); }
        private void AttachmentListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (AttachmentListBox.SelectedItem is ManualAttachment attachment && File.Exists(attachment.FilePath)) Process.Start(new ProcessStartInfo(attachment.FilePath) { UseShellExecute = true }); }

        private void SelectCategory(long categoryId) { ManualCategoryComboBox.SelectedItem = categories.FirstOrDefault(category => category.CategoryId == categoryId); }

        private void LoadManualCategories()
        {
            categories.Clear();
            const string query = @"SELECT CATEGORY_ID, PARENT_CATEGORY_ID, CATEGORY_NM, SORT_ORD FROM MODUWMNL_CATEGORY WHERE USE_YN = 'Y' ORDER BY PARENT_CATEGORY_ID, SORT_ORD, CATEGORY_NM";
            try
            {
                using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    connection.Open(); using (MySqlDataReader reader = command.ExecuteReader()) while (reader.Read())
                        categories.Add(new ManualCategory(Convert.ToInt64(reader["CATEGORY_ID"]), reader["PARENT_CATEGORY_ID"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["PARENT_CATEGORY_ID"]), reader["CATEGORY_NM"].ToString(), Convert.ToInt32(reader["SORT_ORD"])));
                }
            }
            catch (MySqlException) { }
            BuildCategoryMetadata(); ManualCategoryComboBox.ItemsSource = categories; BuildCategoryTree();
        }

        private void BuildCategoryMetadata()
        {
            foreach (ManualCategory category in categories)
            {
                List<ManualCategory> lineage = new List<ManualCategory>(); ManualCategory current = category;
                while (current != null) { lineage.Add(current); current = current.ParentCategoryId.HasValue ? categories.FirstOrDefault(item => item.CategoryId == current.ParentCategoryId.Value) : null; }
                lineage.Reverse(); category.DisplayPath = string.Join(" / ", lineage.Select(item => item.Name));
                category.SortKey = string.Join("/", lineage.Select(item => item.SortOrder.ToString("D8")));
            }
        }

        private void BuildCategoryTree()
        {
            CategoryTreeView.Items.Clear(); TreeViewItem root = new TreeViewItem { Header = "전체", Tag = null, IsSelected = selectedCategoryId == null };
            CategoryTreeView.Items.Add(root);
            foreach (ManualCategory category in categories.Where(category => category.ParentCategoryId == null).OrderBy(category => category.SortOrder).ThenBy(category => category.Name))
                root.Items.Add(CreateCategoryTreeItem(category));
            if (CategoryTreeView.SelectedItem == null) root.IsSelected = true;
        }

        private TreeViewItem CreateCategoryTreeItem(ManualCategory category)
        {
            TreeViewItem node = new TreeViewItem { Header = category.Name, Tag = category, IsSelected = category.CategoryId == selectedCategoryId };
            foreach (ManualCategory child in categories.Where(item => item.ParentCategoryId == category.CategoryId).OrderBy(item => item.SortOrder).ThenBy(item => item.Name)) node.Items.Add(CreateCategoryTreeItem(child));
            return node;
        }

        private string ShowTextInputDialog(string title, string label)
        {
            Window dialog = new Window { Title = title, Width = 360, Height = 170, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Window.GetWindow(this) };
            TextBox input = new TextBox { Margin = new Thickness(0, 6, 0, 12) }; Button save = new Button { Content = "추가", Width = 70, IsDefault = true }; save.Click += (s, e) => dialog.DialogResult = true;
            Button cancel = new Button { Content = "취소", Width = 70, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            StackPanel panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold }); panel.Children.Add(input);
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(save); buttons.Children.Add(cancel); panel.Children.Add(buttons); dialog.Content = panel;
            return dialog.ShowDialog() == true ? input.Text : string.Empty;
        }

        private void LoadManualItems()
        {
            allItems.Clear();
            const string query = @"SELECT MANUAL_ID, CATEGORY_ID, MANUAL_TITLE, MANUAL_CONTENT, CHECK_QUERY, LSH_DTM, LSH_STF_NO FROM MODUWMNL WHERE USE_YN = 'Y' AND LST_YN = 'Y' ORDER BY MANUAL_TITLE";
            try
            {
                using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    connection.Open(); using (MySqlDataReader reader = command.ExecuteReader()) while (reader.Read())
                    {
                        long categoryId = Convert.ToInt64(reader["CATEGORY_ID"]); ManualCategory category = categories.FirstOrDefault(item => item.CategoryId == categoryId);
                        DutyManualItem item = new DutyManualItem { ManualId = Convert.ToInt64(reader["MANUAL_ID"]) };
                        item.Update(categoryId, category?.DisplayPath ?? "(삭제된 카테고리)", category?.SortKey ?? string.Empty, reader["MANUAL_TITLE"].ToString(), reader["MANUAL_CONTENT"].ToString(), reader["CHECK_QUERY"].ToString(), Array.Empty<ManualAttachment>(), reader["LSH_STF_NO"].ToString(), Convert.ToDateTime(reader["LSH_DTM"])); allItems.Add(item);
                    }
                }
            }
            catch (MySqlException) { }
        }

        private long InsertManual(long categoryId, string title, string content, string checkQuery)
        {
            const string query = @"INSERT INTO MODUWMNL (CATEGORY_ID, MANUAL_TITLE, MANUAL_CONTENT, CHECK_QUERY, USE_YN, LST_YN, FSR_DTM, FSR_STF_NO, FSR_PRGM_NM, FSR_IP_ADDR, LSH_DTM, LSH_STF_NO, LSH_PRGM_NM, LSH_IP_ADDR) VALUES (@CATEGORY_ID, @TITLE, @CONTENT, @QUERY, 'Y', 'Y', NOW(), @STF_NO, 'DutyManualView', @IP, NOW(), @STF_NO, 'DutyManualView', @IP)";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString)) using (MySqlCommand command = new MySqlCommand(query, connection)) { AddManualParameters(command, categoryId, title, content, checkQuery); connection.Open(); command.ExecuteNonQuery(); return command.LastInsertedId; }
        }

        private long UpdateManual(long manualId, long categoryId, string title, string content, string checkQuery)
        {
            const string query = @"UPDATE MODUWMNL SET CATEGORY_ID = @CATEGORY_ID, MANUAL_TITLE = @TITLE, MANUAL_CONTENT = @CONTENT, CHECK_QUERY = @QUERY, LSH_DTM = NOW(), LSH_STF_NO = @STF_NO, LSH_PRGM_NM = 'DutyManualView', LSH_IP_ADDR = @IP WHERE MANUAL_ID = @MANUAL_ID AND USE_YN = 'Y'";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString)) using (MySqlCommand command = new MySqlCommand(query, connection)) { AddManualParameters(command, categoryId, title, content, checkQuery); command.Parameters.AddWithValue("@MANUAL_ID", manualId); connection.Open(); command.ExecuteNonQuery(); return manualId; }
        }

        private static void AddManualParameters(MySqlCommand command, long categoryId, string title, string content, string checkQuery)
        {
            command.Parameters.AddWithValue("@CATEGORY_ID", categoryId); command.Parameters.AddWithValue("@TITLE", title); command.Parameters.AddWithValue("@CONTENT", content); command.Parameters.AddWithValue("@QUERY", checkQuery);
            command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO); command.Parameters.AddWithValue("@IP", SessionContext.IP_ADDRESS);
        }

        private void SaveManualAttachments(long manualId)
        {
            const string disable = @"UPDATE MODU_ATTACH SET USE_YN = 'N', LSH_DTM = NOW(), LSH_STF_NO = @STF_NO WHERE OWNER_TP_CD = 'MANUAL' AND OWNER_ID = @MANUAL_ID AND USE_YN = 'Y'";
            const string insert = @"INSERT INTO MODU_ATTACH (OWNER_TP_CD, OWNER_ID, FILE_NM, FILE_PATH, FILE_EXT, USE_YN, FSR_DTM, FSR_STF_NO, LSH_DTM, LSH_STF_NO) VALUES ('MANUAL', @MANUAL_ID, @FILE_NM, @FILE_PATH, @FILE_EXT, 'Y', NOW(), @STF_NO, NOW(), @STF_NO)";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString)) { connection.Open(); using (MySqlCommand command = new MySqlCommand(disable, connection)) { command.Parameters.AddWithValue("@MANUAL_ID", manualId); command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO); command.ExecuteNonQuery(); } foreach (ManualAttachment attachment in editingAttachments) using (MySqlCommand command = new MySqlCommand(insert, connection)) { command.Parameters.AddWithValue("@MANUAL_ID", manualId); command.Parameters.AddWithValue("@FILE_NM", attachment.DisplayName); command.Parameters.AddWithValue("@FILE_PATH", attachment.FilePath); command.Parameters.AddWithValue("@FILE_EXT", Path.GetExtension(attachment.FilePath)); command.Parameters.AddWithValue("@STF_NO", SessionContext.STF_NO); command.ExecuteNonQuery(); } }
        }

        private IEnumerable<ManualAttachment> LoadManualAttachments(long manualId)
        {
            List<ManualAttachment> attachments = new List<ManualAttachment>(); const string query = @"SELECT FILE_PATH FROM MODU_ATTACH WHERE OWNER_TP_CD = 'MANUAL' AND OWNER_ID = @MANUAL_ID AND USE_YN = 'Y' ORDER BY ATTACH_ID";
            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString)) using (MySqlCommand command = new MySqlCommand(query, connection)) { command.Parameters.AddWithValue("@MANUAL_ID", manualId); connection.Open(); using (MySqlDataReader reader = command.ExecuteReader()) while (reader.Read()) attachments.Add(new ManualAttachment(reader["FILE_PATH"].ToString())); } return attachments;
        }
    }

    public sealed class ManualCategory
    {
        public ManualCategory(long categoryId, long? parentCategoryId, string name, int sortOrder) { CategoryId = categoryId; ParentCategoryId = parentCategoryId; Name = name; SortOrder = sortOrder; }
        public long CategoryId { get; } public long? ParentCategoryId { get; } public string Name { get; } public int SortOrder { get; }
        public string DisplayPath { get; set; } public string SortKey { get; set; }
        public override string ToString() => DisplayPath ?? Name;
    }

    public sealed class DutyManualItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public long ManualId { get; set; } public long CategoryId { get; private set; } public string Category { get; private set; } = ""; public string CategorySortKey { get; private set; } = ""; public string Title { get; private set; } = ""; public string Detail { get; private set; } = ""; public string Query { get; private set; } = ""; public string LastModifiedLabel { get; private set; } = "";
        public ObservableCollection<ManualAttachment> Attachments { get; } = new ObservableCollection<ManualAttachment>();
        public void Update(long categoryId, string category, string categorySortKey, string title, string detail, string query, IEnumerable<ManualAttachment> attachments, string modifiedBy, DateTime modifiedAt) { CategoryId = categoryId; Category = category; CategorySortKey = categorySortKey; Title = title; Detail = detail; Query = query; LastModifiedLabel = $"최종 수정 {modifiedAt:yyyy-MM-dd HH:mm} · {modifiedBy}"; Attachments.Clear(); foreach (ManualAttachment attachment in attachments) Attachments.Add(attachment.Clone()); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
        public bool Contains(string keyword) { return string.IsNullOrWhiteSpace(keyword) || (Category + " " + Title + " " + Detail + " " + Query).IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase) >= 0; }
    }

    public sealed class ManualAttachment
    {
        public ManualAttachment(string filePath) { FilePath = filePath; DisplayName = Path.GetFileName(filePath); ImageVisibility = IsImage ? Visibility.Visible : Visibility.Collapsed; }
        public string FilePath { get; } public string DisplayName { get; } public bool IsImage => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(Path.GetExtension(FilePath).ToLowerInvariant()); public Visibility ImageVisibility { get; }
        public ManualAttachment Clone() => new ManualAttachment(FilePath);
    }
}
