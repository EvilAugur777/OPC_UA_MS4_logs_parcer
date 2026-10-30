using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

			// Добавили новые колонки в заголовок. Сделал их чуть уже, чтобы всё влезло в экран.
			string headerFormat = $"{{0,-{maxFileNameLength}}} | {{1,6}} | {{2,10}} | {{3,10}} | {{4,10}} | {{5,10}} | {{6,12}} | {{7,12}}";
			string header = string.Format(headerFormat, "Файл", "Записей", "Ср. items", "Мин items", "Ср. 100мс", "Мин 100мс", "Медиана (10%)", "Мед. за 100мс");

			Console.WriteLine(header);
			Console.WriteLine(new string('-', header.Length));

			foreach (var file in logFiles)
			{
				// Теперь сохраняем все значения в список для подсчета медианы
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

					// 1. Обычное среднее
					double sum = 0;
					foreach (var val in itemsList) sum += val;
					double avg = sum / count;

					// 2. Минимум
					long min = itemsList.Min();

					// 3. Усеченное среднее (Медиана 10%)
					double truncatedMean = CalculateTruncatedMean(itemsList, 0.10);

					// Пересчет на 100мс
					double avgPer100ms = avg / 600.0;
					double minPer100ms = min / 600.0;
					double truncatedPer100ms = truncatedMean / 600.0;

					// Округляем до целых при выводе (F0)
					Console.WriteLine(string.Format(headerFormat,
						fileName,
						count,
						$"{avg:F0}",
						min,
						$"{avgPer100ms:F0}",
						$"{minPer100ms:F0}",
						$"{truncatedMean:F0}",
						$"{truncatedPer100ms:F0}"));
				}
				else
				{
					Console.WriteLine(string.Format(headerFormat, fileName, 0, "-", "-", "-", "-", "-", "-"));
				}
			}

			Console.WriteLine("\nГотово. Нажмите любую клавишу для выхода...");
			Console.ReadKey();
		}

		// Функция для расчета усеченного среднего (отбрасываем по 10% с начала и конца)
		static double CalculateTruncatedMean(List<long> values, double trimPercent)
		{
			// Сортируем список по возрастанию
			var sorted = values.OrderBy(x => x).ToList();

			// Вычисляем, сколько элементов нужно отбросить с каждой стороны
			int trimCount = (int)(sorted.Count * trimPercent);

			// Берем только среднюю часть (80%)
			var trimmedList = sorted.Skip(trimCount).Take(sorted.Count - (trimCount * 2)).ToList();

			// Если файл настолько мал, что после обрезки ничего не осталось, возвращаем обычное среднее
			if (trimmedList.Count == 0)
				return sorted.Average();

			return trimmedList.Average();
		}
	}
}