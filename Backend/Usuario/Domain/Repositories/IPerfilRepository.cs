public interface IPerfilRepository
{
	// Método que el Dominio necesita para obtener datos.
	Task<Perfil> GetByIdAsync(string userId);
}