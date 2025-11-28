public class Perfil
{
	public string UserId { get; set; } // Identificador único, típicamente el 'sub' de Keycloak
	public string NombreCompleto { get; set; }
	public string Email { get; set; }
	public string Preferencias { get; set; } // Ejemplo de datos del perfil
}