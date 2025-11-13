namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Типы подсистем поезда для обучения
    /// </summary>
    public enum SubsystemType
    {
        /// <summary>
        /// Не определена
        /// </summary>
        None = 0,

        /// <summary>
        /// Система переходных площадок между вагонами
        /// </summary>
        Gangway = 1,

        /// <summary>
        /// Система дверей
        /// </summary>
        Doors = 2,

        /// <summary>
        /// Система отопления, вентиляции и кондиционирования
        /// </summary>
        HVAC = 3,

        /// <summary>
        /// Тормозная система
        /// </summary>
        Brakes = 4,

        /// <summary>
        /// Система освещения
        /// </summary>
        Lighting = 5,

        /// <summary>
        /// Пассажирская информационная система
        /// </summary>
        PassengerInfo = 6,

        /// <summary>
        /// Система видеонаблюдения
        /// </summary>
        CCTV = 7,

        /// <summary>
        /// Электрические системы
        /// </summary>
        Electrical = 8
    }
}
