using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpcUaLogAnalyzer
{
	// Структура для хранения результатов анализа, чтобы не читать файлы с диска дважды
	struct LogReport
	{
		public string FileName;
		public long Count;
		public string AvgItems;
		public long MinItems;
		public string Avg100ms;
		public string Min100ms;
		public string TruncatedMean;
		public string TruncatedPer100ms;
	}

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

			string headerFormat = $"{{0,-{maxFileNameLength}}} | {{1,6}} | {{2,10}} | {{3,10}} | {{4,10}} | {{5,10}} | {{6,12}} | {{7,12}}";
			string header = string.Format(headerFormat, "Файл", "Записей", "Ср. items", "Мин items", "Ср. 100мс", "Мин 100мс", "Медиана (10%)", "Мед. за 100мс");

			Console.WriteLine(header);
			Console.WriteLine(new string('-', header.Length));

			// Список для хранения отчетов по каждому файлу
			var reportsList = new List<LogReport>();

			foreach (var file in logFiles)
			{
				List<long> itemsList = new List<long>();

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
									itemsList.Add(itemsVal);
								}
							}
						}
					}
				}

				string fileName = Path.GetFileName(file);

				if (itemsList.Count > 0)
				{
					long count = itemsList.Count;

					double sum = 0;
					foreach (var val in itemsList) sum += val;
					double avg = sum / count;

					long min = itemsList.Min();

					double truncatedMean = CalculateTruncatedMean(itemsList, 0.10);

					double avgPer100ms = avg / 600.0;
					double minPer100ms = min / 600.0;
					double truncatedPer100ms = truncatedMean / 600.0;

					// Выводим данные в консоль
					Console.WriteLine(string.Format(headerFormat,
						fileName,
						count,
						$"{avg:F0}",
						min,
						$"{avgPer100ms:F0}",
						$"{minPer100ms:F0}",
						$"{truncatedMean:F0}",
						$"{truncatedPer100ms:F0}"));

					// Сохраняем в список для CSV
					reportsList.Add(new LogReport
					{
						FileName = fileName,
						Count = count,
						AvgItems = avg.ToString("F0"),
						MinItems = min,
						Avg100ms = avgPer100ms.ToString("F0"),
						Min100ms = minPer100ms.ToString("F0"),
						TruncatedMean = truncatedMean.ToString("F0"),
						TruncatedPer100ms = truncatedPer100ms.ToString("F0")
					});
				}
				else
				{
					Console.WriteLine(string.Format(headerFormat, fileName, 0, "-", "-", "-", "-", "-", "-"));

					reportsList.Add(new LogReport
					{
						FileName = fileName,
						Count = 0,
						AvgItems = "-",
						MinItems = 0,
						Avg100ms = "-",
						Min100ms = "-",
						TruncatedMean = "-",
						TruncatedPer100ms = "-"
					});
				}
			}

			// Логика запроса сохранения в CSV
			Console.Write("\nСохранить отчет в CSV? [y/N] (по умолчанию: No): ");
			string saveChoice = Console.ReadLine()?.Trim().ToLower();

			if (saveChoice == "y" || saveChoice == "yes")
			{
				try
				{
					string csvPath = Path.Combine(folderPath, $"report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

					using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(true)))
					{
						writer.WriteLine("Файл;Записей;Ср. items;Мин items;Ср. 100мс;Мин 100мс;Медиана (10%);Мед. за 100мс");

						foreach (var rep in reportsList)
						{
							string minValStr = rep.Count > 0 ? rep.MinItems.ToString() : "-";
							string min100Str = rep.Count > 0 ? rep.Min100ms : "-";

							writer.WriteLine($"{rep.FileName};{rep.Count};{rep.AvgItems};{minValStr};{rep.Avg100ms};{min100Str};{rep.TruncatedMean};{rep.TruncatedPer100ms}");
						}
					}

					Console.WriteLine($"[Успех] Отчет успешно сохранен в: {csvPath}");

					// НОВАЯ ЛОГИКА: Вопрос об открытии файла
					Console.Write("Открыть созданный отчет? [y/N] (по умолчанию: No): ");
					string openChoice = Console.ReadLine()?.Trim().ToLower();

					if (openChoice == "y" || openChoice == "yes")
					{
						// Используем ProcessStartInfo с UseShellExecute = true, 
						// чтобы Windows сама открыла файл в программе по умолчанию (Excel, Блокнот и т.д.)
						System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
						{
							FileName = csvPath,
							UseShellExecute = true
						});
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[Ошибка] Не удалось обработать файл: {ex.Message}");
				}
			}


			Console.WriteLine("\nГотово. Нажмите любую клавишу для выхода...");
			Console.ReadKey();
		}

		static double CalculateTruncatedMean(List<long> values, double trimPercent)
		{
			var sorted = values.OrderBy(x => x).ToList();
			int trimCount = (int)(sorted.Count * trimPercent);
			var trimmedList = sorted.Skip(trimCount).Take(sorted.Count - (trimCount * 2)).ToList();

			if (trimmedList.Count == 0)
				return sorted.Average();

			return trimmedList.Average();
		}
	}
}
