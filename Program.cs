using System.Diagnostics;
using System.Xml;

namespace ExtractBazis
{

    internal class Program
    {
        // Константы для хранения настроек
        private static string folderPath;
        private static string fileName;
        private static string fullPath;
        private static string innounpPath;
        private static string appFolder;
        private static string[] filesToExclude;
        private static string targetDirectoryForBazis;
        private static string targetDirectoryForTmp;
        private static int scanRefreshTime;
        private static int scanCountTime;
        private static bool useShellExecute;
        private static bool createNoWindow;
        internal static string logPath;
        private static string innounpCommand;
        // Таймеры
        private static Stopwatch clockIsUpdateExist = new();
        private static Stopwatch clockInnounpExe = new();
        private static Stopwatch clockAllTime = new();

        static void Main(string[] args)
        {
            LoadConfiguration("config.xml");
            // Обновление fullPath после загрузки настроек
            fullPath = Path.Combine(folderPath, fileName);
            // Формирование команду для innounp
            innounpCommand = $"innounp.exe -x \"{fullPath}\"";

            clockAllTime.Reset();
            clockIsUpdateExist.Reset();
            clockInnounpExe.Reset();

            CleanDirectory(targetDirectoryForBazis); // Очистка папки Bazis перед запуском программы
            CleanDirectory(targetDirectoryForTmp); // Очистка папки TMP перед запуском программы

            clockAllTime.Start();
            if (IsUpdateExist()) ExtractToFolder();
            else IsUpdateExist();


            clockAllTime.Stop();
            Logger.WriteLog($"Время затраченное на все операции - {clockAllTime.Elapsed.Seconds} секунд");
            clockAllTime.Reset();
        }
        private static bool IsUpdateExist() // Проверка файла в директории
        {
            clockIsUpdateExist.Start();
            Logger.WriteLog($"Проверка наличия файла в папке '{folderPath}' - запущена ");

            if (File.Exists(fullPath))
            {
                Logger.WriteLog($"Файл {fileName} - существует");
                clockIsUpdateExist.Stop();
                Logger.WriteLog($"Время затраченное на операцию IsUpdateExist - {clockIsUpdateExist.Elapsed.Seconds} секунд");
                clockIsUpdateExist.Reset();
                return true;
            }
            else
            {
                Logger.WriteLog($"Файл {fileName} - отсутствует");
                clockIsUpdateExist.Stop();
                Logger.WriteLog($"Время затраченное на операцию IsUpdateExist - {clockIsUpdateExist.Elapsed.Seconds} секунд");
                clockIsUpdateExist.Reset();
                return false;
            }
        }
        private static void ExtractToFolder()
        {
            clockInnounpExe.Start();
            Cmd();
            clockInnounpExe.Stop();
            Logger.WriteLog($"Время затраченное на операцию innounp.exe - {clockInnounpExe.Elapsed.Seconds} секунд");
            clockInnounpExe.Reset();

            try
            {
                // Получение файлов из исходной папки
                var files = Directory.GetFiles(innounpPath);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);

                    // Проверка, не входит ли файл в список исключений
                    if (Array.IndexOf(filesToExclude, fileName) == -1)
                    {
                        string targetFile = Path.Combine(targetDirectoryForTmp, fileName);

                        File.Move(file, targetFile);
                    }
                }
                // Получаем все директории (папки) из исходной папки
                var directories = Directory.GetDirectories(innounpPath);

                foreach (var directory in directories)
                {
                    string folderName = Path.GetFileName(directory);

                    if (folderName == "{app}")
                    {
                        // Копирование папку {app} в новую директорию
                        string copyTargetPath = Path.Combine(folderPath, "Bazis");
                        CopyDirectory(directory, copyTargetPath);

                        // Перемещение исходной папки {app} в целевую папку
                        string targetFolderPath = Path.Combine(targetDirectoryForTmp, folderName);
                        Directory.Move(directory, targetFolderPath);
                    }
                    else
                    {
                        // Перемещение остальных папок
                        string targetFolderPath = Path.Combine(targetDirectoryForTmp, folderName);
                        Directory.Move(directory, targetFolderPath);
                    }
                }

                Logger.WriteLog($"Файлы и папки успешно перемещены.");
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"[Ошибка]: {ex.Message}");
            }
        }
        private static void Cmd()
        {
            ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe")
            {
                Verb = "runas",
                UseShellExecute = useShellExecute,
                CreateNoWindow = createNoWindow,
                WorkingDirectory = innounpPath,
                Arguments = "/C " + innounpCommand
            };
            try
            {
                // Запуск процесса и сохранение ссылки на него
                using (Process process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"[Ошибка] {ex.ToString()}");   
            }
        }
        private static void CleanDirectory(string directoryPath)
        {
            try
            {
                // Удаление всеХ файлов в указанной директории
                var files = Directory.GetFiles(directoryPath);
                foreach (var file in files)
                {
                    var fileAttributes = File.GetAttributes(file);
                    // Является ли файл системным или скрытым
                    if ((fileAttributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        (fileAttributes & FileAttributes.System) == FileAttributes.System)
                    {
                        Logger.WriteLog($"Файл [{file}] пропущен, так как он является системным или скрытым");
                        continue;
                    }

                    File.Delete(file);
                    Logger.WriteLog($"Файл [{file}] - Удалён");
                }

                // Удаление всеъ подкаталогов и их содержимое
                var directories = Directory.GetDirectories(directoryPath);
                foreach (var subDirectory in directories)
                {
                    var dirAttributes = File.GetAttributes(subDirectory);
                    // Является ли папка системной или скрытой
                    if ((dirAttributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        (dirAttributes & FileAttributes.System) == FileAttributes.System)
                    {
                        Logger.WriteLog($"Директория [{subDirectory}] пропущена, так как она является системной или скрытой");
                        continue;
                    }

                    Directory.Delete(subDirectory, true);
                    Logger.WriteLog($"Директория [{subDirectory}] - Удалена");
                }

                Logger.WriteLog($"Содержимое директории [{directoryPath}] - Очищено");
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"[Ошибка] {ex}");
            }
        }
        // Метод для рекурсивного копирования папок
        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string targetFilePath = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, targetFilePath, true);
            }

            // Рекурсия по директории
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string newDestinationDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, newDestinationDir);
            }
        }
        private static void LoadConfiguration(string configPath)
        {
            try
            {
                // Существует ли файл конфигурации
                if (!File.Exists(configPath))
                {
                    Logger.WriteLog($"Файл конфигурации {configPath} не найден.");
                    return;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(configPath);

                // Зачения по умолчанию
                folderPath = doc.SelectSingleNode("/Configuration/Paths/FolderPath")?.InnerText ?? @"I:\iT\Bazis Update Remote";
                fileName = doc.SelectSingleNode("/Configuration/Paths/FileName")?.InnerText ?? "UpdateBazis2024.exe";
                innounpPath = doc.SelectSingleNode("/Configuration/Paths/InnounpPath")?.InnerText ?? @"I:\iT\Bazis Update Remote\Innounp";
                appFolder = doc.SelectSingleNode("/Configuration/Paths/AppFolder")?.InnerText ?? @"I:\iT\Bazis Update Remote\{app}";
                targetDirectoryForBazis = doc.SelectSingleNode("/Configuration/Paths/TargetDirectoryForBazis")?.InnerText ?? @"I:\iT\Bazis Update Remote\Bazis";
                targetDirectoryForTmp = doc.SelectSingleNode("/Configuration/Paths/TargetDirectoryForTmp")?.InnerText ?? @"I:\iT\Bazis Update Remote\BazisTMP";
                logPath = doc.SelectSingleNode("/Configuration/Paths/LogPath")?.InnerText ?? @"C:\Users\/*NAME*/\Documents\Logs\logBazis.txt";

                // Чтение файлов, которые нужно исключить
                var fileNodes = doc.SelectNodes("/Configuration/FilesToExclude/File");
                var fileList = new List<string>();
                foreach (XmlNode node in fileNodes)
                {
                    if (node != null && !string.IsNullOrWhiteSpace(node.InnerText))
                    {
                        fileList.Add(node.InnerText);
                    }
                }
                filesToExclude = fileList.ToArray();

                // Чтение булевых настроек
                useShellExecute = bool.Parse(doc.SelectSingleNode("/Configuration/BooleanSettings/UseShellExecute")?.InnerText ?? "true");
                createNoWindow = bool.Parse(doc.SelectSingleNode("/Configuration/BooleanSettings/CreateNoWindow")?.InnerText ?? "false");

                Logger.WriteLog("Конфигурация загружена успешно.");
            }
            catch (Exception ex)
            {
                Logger.WriteLog($"Ошибка при загрузке конфигурации: {ex.Message}");
            }
        }

    }

    public static class Logger
    {
        public static void WriteLog(string message)
        {
            using (StreamWriter writer = new StreamWriter(Program.logPath, true))
            {
                writer.WriteLine($"{DateTime.Now} : {message}");
            }
        }
    }
}
