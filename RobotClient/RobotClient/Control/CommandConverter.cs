using System;
using Newtonsoft.Json;

namespace RobotClient.Core
{
    /// <summary>
    /// Класс для конвертации JSON команд в структуры данных
    /// </summary>
    public class CommandConverter
    {
        private const string COMMAND_PREFIX = "COMMAND!";

        /// <summary>
        /// Парсит JSON строку команды и возвращает структуру Command
        /// </summary>
        /// <param name="jsonString">JSON строка в формате "COMMAND!{jsonData}"</param>
        /// <returns>Структура Command или null при ошибке</returns>
        public static Command? ParseCommand(string jsonString)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonString))
                {
                    Console.WriteLine("Ошибка: пустая строка команды");
                    return null;
                }

                // Проверяем префикс COMMAND!
                if (!jsonString.StartsWith(COMMAND_PREFIX))
                {
                    Console.WriteLine($"Ошибка: неверный префикс команды. Ожидается '{COMMAND_PREFIX}'");
                    return null;
                }

                // Извлекаем JSON часть
                string jsonData = jsonString.Substring(COMMAND_PREFIX.Length);
                
                if (string.IsNullOrEmpty(jsonData))
                {
                    Console.WriteLine("Ошибка: отсутствуют JSON данные");
                    return null;
                }

                // Парсим JSON в структуру Command
                Command command = JsonConvert.DeserializeObject<Command>(jsonData);
                
                Console.WriteLine($"Команда успешно разобрана: " +
                    $"Left({command.leftStickValue.x:F2}, {command.leftStickValue.y:F2}), " +
                    $"Right({command.rightStickValue.x:F2}, {command.rightStickValue.y:F2}), " +
                    $"Camera: {command.cameraAngle:F1}°");

                return command;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Ошибка парсинга JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Неожиданная ошибка при парсинге команды: {ex.Message}");
                return null;
            }
        }

      
        /// <summary>
        /// Проверяет, является ли строка валидной командой
        /// </summary>
        /// <param name="jsonString">Строка для проверки</param>
        /// <returns>True если строка является валидной командой</returns>
        public static bool IsValidCommand(string jsonString)
        {
            return ParseCommand(jsonString).HasValue;
        }      
    }

    /// <summary>
    /// Структура для представления 2D вектора (аналог Unity Vector2)
    /// </summary>
    [Serializable]
    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Длина вектора
        /// </summary>
        public float magnitude => (float)Math.Sqrt(x * x + y * y);

        /// <summary>
        /// Нормализованный вектор
        /// </summary>
        public Vector2 normalized
        {
            get
            {
                float mag = magnitude;
                if (mag > 0.00001f)
                    return new Vector2(x / mag, y / mag);
                return new Vector2(0, 0);
            }
        } 
    }

    /// <summary>
    /// Структура команды управления
    /// </summary>
    [Serializable]
    public struct Command
    {
        /// <summary>
        /// Значение левого стика (обычно движение)
        /// </summary>
        public Vector2 leftStickValue;

        /// <summary>
        /// Значение правого стика (обычно поворот)
        /// </summary>
        public Vector2 rightStickValue;

        /// <summary>
        /// Угол наклона камеры в градусах
        /// </summary>
        public float cameraAngle;     
    }
} 