namespace ClinicQueue.Services
{
    public interface IDoctorService
    {
        Task<int?> GetDoctorStatusAsync(string doctorId);
        Task SetDoctorStatusAsync(string doctorId, int status);
    }
}
