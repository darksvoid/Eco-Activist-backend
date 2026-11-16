using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Entities;

public class Category : AggregateRoot
{
    public const int NameMaxLength = 100;

    public string Name { get; private set; } = null!;
    public int EcoPointsReward { get; private set; }

    private Category() { }

    public Category(Guid id, string name, int ecoPointsReward) : base(id)
    {
        SetName(name);
        SetReward(ecoPointsReward);
    }

    public static Category Create(string name, int ecoPointsReward) => new(Guid.NewGuid(), name, ecoPointsReward);

    public void Update(string name, int ecoPointsReward)
    {
        SetName(name);
        SetReward(ecoPointsReward);
    }

    private void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new ArgumentException($"Category name must not exceed {NameMaxLength} characters.", nameof(name));
        Name = trimmed;
    }

    private void SetReward(int ecoPointsReward)
    {
        if (ecoPointsReward < 0)
            throw new ArgumentOutOfRangeException(nameof(ecoPointsReward), ecoPointsReward, "EcoPointsReward cannot be negative.");
        EcoPointsReward = ecoPointsReward;
    }
}
