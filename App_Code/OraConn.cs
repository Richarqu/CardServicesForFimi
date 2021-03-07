using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using Oracle.ManagedDataAccess.Client;

/// <summary>
/// Summary description for OraConn
/// </summary>
public class OraConn
{
    public OracleConnection conn;
    public OracleCommand cmd;
    public int num_rows;
    public OraConn(string query, string conType = "oraConn")
    {
        try
        {
            conn = new OracleConnection();
            string config = ConfigurationManager.AppSettings[conType];

            conn.ConnectionString = OneConfig.Text.Get(config);
            //"data source=(DESCRIPTION =(ADDRESS_LIST =(ADDRESS = (PROTOCOL = TCP)(HOST=10.0.42.61)(PORT = 1590)))(CONNECT_DATA =(SID = HOBANK)));user id=transapp;password=yemichigordayo140210;";

            conn.Open();
            cmd = new OracleCommand();
            cmd.Connection = conn;
            cmd.CommandText = query;
            cmd.CommandType = CommandType.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
    public DataSet query(string tblName)
    {
        num_rows = 0;
        try
        {
            OracleDataAdapter res = new OracleDataAdapter();
            res.SelectCommand = cmd;
            res.TableMappings.Add("Table", tblName);
            DataSet ds = new DataSet();
            res.Fill(ds);
            num_rows = ds.Tables[0].Rows.Count;
            close();
            return ds;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return new DataSet();
        }
    }
    public int query()
    {
        try
        {
            int j = cmd.ExecuteNonQuery();
            close();
            return j;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return -1;
        }
    }
    public void close()
    {
        try
        {
            conn.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
    public void addparam(string key, object val)
    {
        try
        {
            OracleParameter prm = this.cmd.Parameters.Add(key, val);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}