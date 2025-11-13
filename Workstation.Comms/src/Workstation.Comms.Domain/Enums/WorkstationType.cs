namespace Workstation.Comms.Domain.Enums
{
    /// <summary>
    /// Тип рабочего места по способу взаимодействия
    /// </summary>
    public enum WorkstationType
    {
        /// <summary>
        /// Только сенсорная панель
        /// </summary>
        Touch = 1,

        /// <summary>
        /// Только VR
        /// </summary>
        VR = 2,

        /// <summary>
        /// Гибридный режим (Touch + VR)
        /// </summary>
        Hybrid = 3
    }
}
