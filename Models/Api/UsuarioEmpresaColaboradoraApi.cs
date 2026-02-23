namespace RazorIdentity.Models.Api;

public class UsuarioEmpresaColaboradoraApi
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int EmpresaColaboradoraId { get; set; }
    public int? RolId { get; set; }
    public int? DisciplinaId { get; set; }
}
