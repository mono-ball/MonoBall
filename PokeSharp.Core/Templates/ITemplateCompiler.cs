namespace PokeSharp.Core.Templates;

/// <summary>
///     Compiles data layer entities into EntityTemplates for ECS runtime.
///     Acts as the bridge between static data (EF Core) and runtime templates.
/// </summary>
/// <typeparam name="TEntity">Data layer entity type</typeparam>
public interface ITemplateCompiler<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     Compile a single entity from the data layer into an EntityTemplate.
    /// </summary>
    /// <param name="entity">Entity to compile</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compiled entity template</returns>
    Task<EntityTemplate> CompileAsync(
        TEntity entity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Compile multiple entities into templates efficiently (batch operation).
    /// </summary>
    /// <param name="entities">Entities to compile</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compiled templates</returns>
    Task<IEnumerable<EntityTemplate>> CompileBatchAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Validate that a data layer entity can be compiled into a valid template.
    /// </summary>
    /// <param name="entity">Entity to validate</param>
    /// <returns>True if entity can be compiled</returns>
    bool Validate(TEntity entity);

    /// <summary>
    ///     Check if template compilation is supported for the given entity type.
    /// </summary>
    /// <typeparam name="T">Entity type to check</typeparam>
    /// <returns>True if compilation is supported</returns>
    bool SupportsType<T>()
        where T : class;

    /// <summary>
    ///     Register a custom compiler for this entity type.
    ///     Allows extensibility for modding and custom data types.
    /// </summary>
    /// <param name="compilationFunc">Compiler function</param>
    void RegisterCompiler(Func<TEntity, EntityTemplate> compilationFunc);
}
