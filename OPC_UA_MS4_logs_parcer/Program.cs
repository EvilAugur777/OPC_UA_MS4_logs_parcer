using System;
using System.IO;
using System.Text.RegularExpressions;

namespace OpcUaLogAnalyzer
{
	class Program
	{
		static readonly Regex LogRegex = new Regex(
			@"OPCUA Client\.\s*St=""[^""]*"".*?items=(\d+)\(\d+,\d+,\d+\)",
			RegexOptions.Compiled);

		static void Main(string[] args)
		{
			Console.WriteLine("Введите полный путь к папке с логами:");
			string folderPath = Console.ReadLine();

			if (string.IsNullOrWhiteSpace(folderPath))
			{
				Console.WriteLine("Путь не введен. Нажмите любую клавишу для выхода...");
				Console.ReadKey();
				return;
			}

			folderPath = folderPath.Trim().Trim('"');

			if (!Directory.Exists(folderPath))
			{
				Console.WriteLine($"Ошибка: Папка по пути '{folderPath}' не найдена!");
				Console.ReadKey();
				return;
			}

			var logFiles = Directory.GetFiles(folderPath, "*.txt");

			if (logFiles.Length == 0)
			{
				Console.WriteLine("В указанной папке не найдено файлов с расширением .txt");
				Console.ReadKey();
				return;
			}

			Console.WriteLine($"\nНайдено файлов: {logFiles.Length}. Начинаю обработку...\n");

			int maxFileNameLength = "Файл".Length;
			foreach (var file in logFiles)
			{
				int len = Path.GetFileName(file).Length;
				if (len > maxFileNameLength) maxFileNameLength = len;
			}
			maxFileNameLength += 2;

			// Сузил колонки с числами до 15 символов, так как теперь они короткие
			string headerFormat = $"{{0,-{maxFileNameLength}}} | {{1,10}} | {{2,15}} | {{3,15}} | {{4,15}} | {{5,15}}";
			string header = string.Format(headerFormat, "Файл", "Записей", "Среднее items", "Мин items", "Ср. за 100мс", "Мин за 100мс");

			Console.WriteLine(header);
			Console.WriteLine(new string('-', header.Length));

			foreach (var file in logFiles)
			{
				long count = 0;
				double sum = 0;
				long min = long.MaxValue;

				using (var reader = new StreamReader(file, System.Text.Encoding.UTF8))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						var match = LogRegex.Match(line);
						if (match.Success)
						{
							if (long.TryParse(match.Groups[1].Value, out long itemsVal))
							{
								if (itemsVal > 0)
								{
									count++;
									sum += itemsVal;
									if (itemsVal < min) min = itemsVal;
								}
							}
						}
					}
				}

				string fileName = Path.GetFileName(file);

				if (count > 0)
				{
					double avg = sum / count;
					double avgPer100ms = avg / 600.0;
					double minPer100ms = min / 600.0;

					// ИЗМЕНЕНИЕ: передаем строки, явно форматируя их через "F0" (0 знаков после запятой)
					Console.WriteLine(string.Format(headerFormat,
						fileName,
						count,
						$"{avg:F0}",
						min,
						$"{avgPer100ms:F0}",
						$"{minPer100ms:F0}"));
				}
				else
				{
					Console.WriteLine(string.Format(headerFormat, fileName, 0, "-", "-", "-", "-"));
				}
			}

			Console.WriteLine("\nГотово. Нажмите любую клавишу для выхода...");
			Console.ReadKey();
		}
	}
}