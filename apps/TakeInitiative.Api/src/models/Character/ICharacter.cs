namespace TakeInitiative.Api.Models;
public interface ICharacter
{
    public string Name { get; set; }
    public CharacterHealth? Health { get; set; }
    public CharacterInitiative Initiative { get; set; }
    public int? ArmorClass { get; set; }
}
