using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using MySql.Data.MySqlClient;
using IniParser;
using IniParser.Model;

class Program
{
    static string configFilePath = "config.ini";
    static FileIniDataParser parser = new FileIniDataParser();
    static IniData config;
    static void Main()
    {
        if (!File.Exists(configFilePath))
        {
            Configure();
        }
        else
        {
            LoadConfig();
        }
        DisplayConfig();
        while (true)
        {
            bool updateSuccessful = UpdateDatabase();
            if (updateSuccessful)
            {
                Console.WriteLine("数据库更新成功。");
            }
            else
            {
                Console.WriteLine("更新数据库时出错。");

            }
            Console.WriteLine("\n是否要更改配置?");
            Console.WriteLine("1. 更改进程名称");
            Console.WriteLine("2. 更改数据库名称");
            Console.WriteLine("3. 更改数据库用户名");
            Console.WriteLine("4. 更改数据库密码");
            Console.WriteLine("5. 更改外网URL (例如：nj.s1.6net.plus:9999)");
            Console.WriteLine("Y. 退出并重新尝试更新数据库");
            Console.WriteLine("N. 退出并不再尝试更新");
            string input = Console.ReadLine().ToUpper();
            if (input == "Y")
            {
                SaveConfig();
                continue;
            }
            else if (input == "N")
            {
                break;
            }
            else
            {
                ChangeConfig(input);
            }
        }
    }

    static void Configure()
    {
        config = new IniData();
        Console.Write("请输入进程名称: ");
        config["Settings"]["processName"] = Console.ReadLine();
        Console.Write("请输入MySQL数据库名称: ");
        config["Settings"]["mysqlDatabase"] = Console.ReadLine();
        Console.Write("请输入MySQL用户名: ");
        config["Settings"]["mysqlUser"] = Console.ReadLine();
        Console.Write("请输入MySQL密码: ");
        config["Settings"]["mysqlPassword"] = Console.ReadLine();
        Console.Write("请输入外网URL (例如：nj.s1.6net.plus:9999): ");
        config["Settings"]["externalUrl"] = Console.ReadLine();
        SaveConfig();
    }

    static void DisplayConfig()
    {
        LoadConfig(); // Ensure the config is loaded from the file

        // Mapping from English keys to Chinese descriptions
        var keyToChinese = new Dictionary<string, string>
        {
            { "processName", "进程名称" },
            { "mysqlDatabase", "MySQL数据库名称" },
            { "mysqlUser", "MySQL用户名" },
            { "mysqlPassword", "MySQL密码" },
            { "externalUrl", "外网URL" }
        };

        Console.WriteLine("当前配置:");

        foreach (var section in config.Sections)
        {
            foreach (var key in section.Keys)
            {
                string chineseName;
                if (keyToChinese.TryGetValue(key.KeyName, out chineseName))
                {
                    Console.WriteLine($"{chineseName} = {key.Value}");
                }
                else
                {
                    Console.WriteLine($"{key.KeyName} = {key.Value}");
                }
            }
        }
        Console.WriteLine("——————————————————————————————————\n");
    }
    static void LoadConfig()
    {
        config = parser.ReadFile(configFilePath);
    }

    static void SaveConfig()
    {
        parser.WriteFile(configFilePath, config);
    }

    static void ChangeConfig(string input)
    {
        switch (input)
        {
            case "1":
                Console.Write("请输入新的进程名称 不要输入.exe后缀: ");
                config["Settings"]["processName"] = Console.ReadLine();
                break;
            case "2":
                Console.Write("请输入新的MySQL数据库名称: ");
                config["Settings"]["mysqlDatabase"] = Console.ReadLine();
                break;
            case "3":
                Console.Write("请输入新的MySQL用户名: ");
                config["Settings"]["mysqlUser"] = Console.ReadLine();
                break;
            case "4":
                Console.Write("请输入新的MySQL密码: ");
                config["Settings"]["mysqlPassword"] = Console.ReadLine();
                break;
            case "5":
                Console.Write("请输入新的外网URL (例如：nj.s1.6net.plus:9999) 无需加http");
                config["Settings"]["externalUrl"] = Console.ReadLine();
                break;
            default:
                Console.WriteLine("无效的选项。");
                break;
        }
        SaveConfig();
    }

    static bool UpdateDatabase()
    {
        string processName = config["Settings"]["processName"];
        string mysqlServer = "localhost";
        string mysqlDatabase = config["Settings"]["mysqlDatabase"];
        string mysqlUser = config["Settings"]["mysqlUser"];
        string mysqlPassword = config["Settings"]["mysqlPassword"];
        string externalUrl = config["Settings"]["externalUrl"];
        string table = "wp_options";
        string siteurlField = "siteurl";
        string homeField = "home";
        // Check if netplus.exe is running
        bool isNetplusRunning = Process.GetProcessesByName(processName).Any();
        // Determine the new value for siteurl and home
        string newValue;
        if (isNetplusRunning)
        {
            Console.WriteLine("外网模式启动");
            newValue = $"http://{externalUrl}";
        }
        else
        {
            Console.WriteLine("未检测到指定进程 局域网模式启动");
            newValue = GetLocalIPAddress();
            Console.WriteLine($"局域网IP地址: {newValue}");
        }
        // Update the database
        string connStr = $"server={mysqlServer};user={mysqlUser};database={mysqlDatabase};port=3306;password={mysqlPassword}";
        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = $"UPDATE {table} SET option_value=@newValue WHERE option_name=@siteurlField OR option_name=@homeField";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@newValue", newValue);
                    cmd.Parameters.AddWithValue("@siteurlField", siteurlField);
                    cmd.Parameters.AddWithValue("@homeField", homeField);
                    cmd.ExecuteNonQuery();
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"更新数据库时出错: {ex.Message}");
            return false;
        }
    }

    static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && ip.ToString().StartsWith("192.168"))
            {
                return $"http://{ip.ToString()}";
            }
        }
        throw new Exception("没有找到192.168.x.x范围内的IPv4地址!");
    }
}

