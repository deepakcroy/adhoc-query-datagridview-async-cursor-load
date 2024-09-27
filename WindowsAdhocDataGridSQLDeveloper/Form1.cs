using Oracle.DataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace WindowsAdhocDataGridSQLDeveloper
{
    public partial class Form1 : Form
    {
        //public static string connectionString = "Server=localhost;Port=3306;Database=tcs_dpdc_mw_prod;Uid=deepak;Pwd=821216;charset=utf8;";
        static string host = "localhost";
        static string port = "1521";
        static string sid = "orcl";
        static string user = "user";
        static string pwd = "pwd";
        public static string connectionString = "Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST= " + host + " )(PORT= " + port + " )))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME= " + sid + " )));User Id= " + user + " ;Password=" + pwd + ";";

        private List<object[]> data = new List<object[]>();
        private int loadedRows = 0;
        private const int batchSize = 50; // Number of rows to load at a time

        private OracleConnection connection; // Class-level variable for connection
        private OracleCommand command; // Class-level variable for command
        private OracleDataReader reader; // Class-level variable for reader

        private bool isLoading = false; // Flag to prevent multiple loads
        private System.Timers.Timer scrollTimer; // Timer for debouncing scroll

        private object _locker = new object();

        public Form1()
        {
            InitializeComponent();

            this.dataGridView1.VirtualMode = true;
            this.dataGridView1.CellValueNeeded += DataGridView1_CellValueNeeded;
            this.dataGridView1.Scroll += DataGridView1_Scroll; // Handle Scroll event
            this.dataGridView1.RowPostPaint += DataGridView1_RowPostPaint; // Subscribe to RowPostPaint



            //this.dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToOrderColumns = false;
            this.dataGridView1.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;

            // this.dataGridView1.SetRedraw(false);
            this.dataGridView1.SetDoubleBuffering(true);



            // Initialize the scroll timer
            scrollTimer = new System.Timers.Timer(100); // Set debounce time (in milliseconds)
            scrollTimer.Elapsed += OnScrollTimerElapsed;
            scrollTimer.AutoReset = false; // Run only once

            // LoadDataAsync(); // Initial load
        }

        private async void LoadDataAsync()
        {
            try
            {
                connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                // Prepare query
                string query = textBox1.Text.Trim();
                command = new OracleCommand(query, connection);
                command.CommandType = CommandType.Text;

                reader = (OracleDataReader)await command.ExecuteReaderAsync(CommandBehavior.Default);

                // Clear previous data and columns
                data.Clear();
                dataGridView1.Columns.Clear();

                // Create columns in the DataGridView
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    dataGridView1.Columns.Add(reader.GetName(i), reader.GetName(i));
                }

                // Load initial batch of data
                await LoadMoreDataAsync(batchSize);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void DataGridView1_Scroll(object sender, ScrollEventArgs e)
        {
            // Check if the user is close to the bottom of the DataGridView
            if (dataGridView1.FirstDisplayedScrollingRowIndex + dataGridView1.DisplayedRowCount(false) >= loadedRows - 5 && !isLoading)
            {
                // Start loading more data
                _ = LoadMoreDataAsync(batchSize);  // Load more data asynchronously
            }
        }

        private async Task LoadMoreDataAsync(int numberOfRows)
        {
            // Check if already loading data
            if (isLoading) return;

            isLoading = true; // Set loading flag to true

            try
            {
                // To ensure that the data loading does not block the UI thread, consider running it in a separate task when loading more data
                await Task.Run(async () =>
                {
                    // Read the next batch of data using the cursor
                    for (int i = 0; i < numberOfRows; i++)
                    {
                        if (await reader.ReadAsync())
                        {
                            object[] rowData = new object[reader.FieldCount];
                            reader.GetValues(rowData);
                            data.Add(rowData);
                            loadedRows++; // Keep track of how many rows have been loaded
                        }
                        else
                        {
                            // No more rows to read, stop loading
                            break;
                        }
                    }
                });

                // Update DataGridView row count on the UI thread
                this.Invoke((MethodInvoker)delegate
                {
                    dataGridView1.RowCount = loadedRows; // Set the row count to the loaded rows
                    dataGridView1.Invalidate(); // Refresh the DataGridView to reflect changes
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                isLoading = false; // Reset loading flag
            }
        }

        private void DataGridView1_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < data.Count)
            {
                e.Value = data[e.RowIndex][e.ColumnIndex];
            }
        }

        private async void OnScrollTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Marshal to the UI thread to check if close to the bottom of the currently loaded rows
            this.Invoke((MethodInvoker)delegate
            {
                // Check if we're near the bottom
                if (dataGridView1.FirstDisplayedScrollingRowIndex + dataGridView1.DisplayedRowCount(false) >= loadedRows - 5)
                {
                    isLoading = true; // Set loading flag
                }
            });

            // Proceed with loading the next batch of data asynchronously outside of the Invoke
            if (isLoading)
            {
                int nextBatchSize = Math.Min(batchSize, 50);
                await LoadMoreDataAsync(nextBatchSize);

                // Update isLoading flag back on the UI thread after async operation completes
                this.Invoke((MethodInvoker)delegate
                {
                    isLoading = false; // Reset loading flag
                });
            }
        }

        private void ResetDataGridView()
        {
            // Clear existing data and columns
            data.Clear();
            loadedRows = 0; // Reset loaded rows count

            // Clear the DataGridView
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ResetDataGridView();
            LoadDataAsync(); // Reload data on button click
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            // Close the reader and connection when the form is closing
            if (reader != null && !reader.IsClosed)
            {
                reader.Close();
            }
            if (command != null)
            {
                command.Dispose();
            }
            if (connection != null)
            {
                connection.Close();
            }
        }
        private void DataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            // Draw row number
            int rowIndex = e.RowIndex + 1; // 1-based index
            Rectangle headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, e.RowBounds.Width, e.RowBounds.Height);

            using (Brush brush = new SolidBrush(dataGridView1.ForeColor))
            {
                e.Graphics.DrawString(rowIndex.ToString(), dataGridView1.Font, brush, headerBounds);
            }
        }

    }
}