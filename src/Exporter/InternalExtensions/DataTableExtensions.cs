using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnvironmentSettingsExporter.InternalExtensions
{
    internal static class DataTableExtensions
    {
        private const string refPattern = @"\$\{([^}]+)\}";

        internal static bool TryGetCellValue(this DataTable settingsTable, int rowIndex, int columnIndex, out string val)
        { return !string.IsNullOrEmpty((val = settingsTable.GetCellValue(rowIndex, columnIndex))); }

        internal static void ResolveReferences(this DataTable settingsTable)
        {
            var prevColour = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var refRegex = new Regex(refPattern);
                // Start with the first column past default values
                for (int columnIndex = Constants.DEFAULTVALUECOLUMN + 1;
                     columnIndex < settingsTable.Columns.Count && !settingsTable.Rows[Constants.FILENAMEROW].IsNull(columnIndex);
                     columnIndex++)
                {
                    if (!settingsTable.TryGetCellValue(Constants.GENERATEFILEROW, columnIndex, out var val)
                        || "yes".Equals(val, System.StringComparison.InvariantCultureIgnoreCase)
                        || "true".Equals(val, System.StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine($"Resolving references in '{settingsTable.GetCellValue(Constants.ENVIRONMENTNAMEROW, columnIndex)}'");

                        var resolvedItems = new List<Tuple<string, int, string>>();
                        var unresolvedItems = new List<Tuple<string, int, string>>();
                        var rowCount = settingsTable.Rows.Count;
                        for (var rowIndex = Constants.FIRSTVALUEROW; rowIndex < rowCount; rowIndex++)
                        {
                            var sn = settingsTable.GetCellValue(rowIndex, Constants.SETTINGSNAMECOLUMN);
                            if (!string.IsNullOrEmpty(sn))
                            {
                                var sv = settingsTable.GetSettingValue(rowIndex, columnIndex);
                                var t = new Tuple<string, int, string>(sn, rowIndex, sv);
                                if (refRegex.IsMatch(sv))
                                { unresolvedItems.Add(t); }
                                else
                                { resolvedItems.Add(t); }
                            }
                        }

                        //Resolving the references. Continue as long as there are unresolved items which we can resolve
                        bool didResolveSomething = true;
                        while (unresolvedItems.Count > 0 && didResolveSomething)
                        {
                            didResolveSomething = false;
                            foreach (var item in unresolvedItems)
                            {
                                var result = refRegex.Replace(item.Item3, m =>
                                {
                                    if (resolvedItems.Exists(ri => ri.Item1.Equals(m.Groups[1].Value, StringComparison.InvariantCultureIgnoreCase)))
                                    {
                                        didResolveSomething = true;
                                        return resolvedItems.Find(ri => ri.Item1.Equals(m.Groups[1].Value, StringComparison.InvariantCultureIgnoreCase)).Item3;
                                    }
                                    return m.Value;
                                });
                                if (didResolveSomething)
                                {
                                    var newItem = new Tuple<string, int, string>(item.Item1, item.Item2, result);
                                    unresolvedItems.Remove(item);
                                    if (refRegex.IsMatch(result)) //we resolved something, but the cell requires more work.
                                    {
                                        Console.WriteLine($"\tPartially resolved reference(s) in '{item.Item1}'");
                                        unresolvedItems.Add(newItem);
                                    }
                                    else //completely resolved!
                                    {
                                        Console.WriteLine($"\tResolved reference(s) in '{item.Item1}'");
                                        resolvedItems.Add(newItem);
                                    }
                                    break; //Modified the collection, restart the loop
                                }
                            }
                        }

                        if (unresolvedItems.Count > 0)
                        { throw new ApplicationException($"Settings file contains unresolved or circular references in: '{string.Join("', '", unresolvedItems.Select(t => t.Item1))}'"); }

                        //update the table
                        foreach (var item in resolvedItems)
                        { settingsTable.Rows[item.Item2][columnIndex] = item.Item3; }
                    }
                }
            }
            finally
            { Console.ForegroundColor = prevColour; }
        }

        #region Copied from DataTableToXmlExporter
        internal static string GetCellValue(this DataTable settingsTable, int rowIndex, int columnIndex)
        {
            if (settingsTable.Rows[rowIndex].IsNull(columnIndex))
            {
                return string.Empty;
            }
            else
            {
                return ((string)settingsTable.Rows[rowIndex][columnIndex]).Trim();
            }
        }

        internal static string GetSettingValue(this DataTable settingsTable, int rowIndex, int columnIndex)
        {
            string value = GetCellValue(settingsTable, rowIndex, columnIndex);

            if (string.IsNullOrEmpty(value))
            {
                value = GetCellValue(settingsTable, rowIndex, Constants.DEFAULTVALUECOLUMN);
            }

            return value;
        }
        #endregion
    }
}