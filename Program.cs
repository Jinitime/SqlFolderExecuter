using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.Data.SqlClient;

public static class SqlFolderExecuter
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: SqlFolderExecuter <localDatabaseName> <folderPath>");
            Console.WriteLine("N.B.: <folderPath> is optional and defaults to the current directory.");
            return;
        }

        string folderPath = args[1];
        string localDatabaseName = args[0];

        string folder = string.IsNullOrWhiteSpace(folderPath) ? Directory.GetCurrentDirectory() : folderPath; //Get current executing folder path
        string connStr = $"Server=localhost;Database={localDatabaseName};Integrated Security=true;Encrypt=True;TrustServerCertificate=True;";

        string result = SqlFolderExecuter.ExecuteSqlScripts(folder, connStr);
        Console.WriteLine(result);
    }


    /// <summary>
    /// Executes all *.sql files in the specified folder (excluding subfolders),
    /// ordered from oldest to newest by last write time.
    /// </summary>
    /// <param name="folderPath">Path to the folder containing .sql files.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <returns>Multiline string with execution results and any errors.</returns>
    public static string ExecuteSqlScripts(string folderPath, string connectionString)
    {
        if (!Directory.Exists(folderPath))
            return $"Error: Folder '{folderPath}' does not exist.";

        // Get all .sql files (top directory only)
        string[] sqlFiles = Directory.GetFiles(folderPath, "*.sql", SearchOption.TopDirectoryOnly);
        if (sqlFiles.Length == 0)
            return $"No *.sql files found in '{folderPath}'.";

        // Sort files by last write time (oldest first)
        Array.Sort(sqlFiles, (a, b) => File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b)));

        var results = new StringBuilder();
        bool anyError = false;

        foreach (string filePath in sqlFiles)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                string script = File.ReadAllText(filePath);
                ExecuteScript(connectionString, script);
                results.AppendLine($"SUCCESS: {fileName}");
            }
            catch (Exception ex)
            {
                anyError = true;
                results.AppendLine($"ERROR: {fileName} - {ex.Message}");
                // Continue with remaining scripts to report all errors
            }
        }

        results.Insert(0, anyError ? "Execution completed with errors.\n" : "All scripts executed successfully.\n");
        return results.ToString();
    }

    private static void ExecuteScript(string connectionString, string script)
    {
        // Split script by "GO" statements (case-insensitive, on its own line)
        var batches = SplitSqlBatches(script);

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            foreach (string batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                using (var command = new SqlCommand(batch, connection))
                {
                    command.CommandType = CommandType.Text;
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        var batches = new List<string>();
        var currentBatch = new StringBuilder();
        using (var reader = new StringReader(script))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    // End of current batch
                    batches.Add(currentBatch.ToString());
                    currentBatch.Clear();
                }
                else
                {
                    currentBatch.AppendLine(line);
                }
            }
            // Add the last batch if not empty
            if (currentBatch.Length > 0)
                batches.Add(currentBatch.ToString());
        }
        return batches;
    }
}