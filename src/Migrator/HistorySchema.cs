using Npgsql;

namespace Migrator;

/// <summary>
/// DDL создания и безопасного upgrade таблицы migration_history.
/// </summary>
public static class HistorySchema
{
    public const string CreateTableSql = """
                                        create table if not exists migration_history (
                                            id serial primary key,
                                            order_number int not null,
                                            file_name text not null,
                                            name text not null,
                                            sql text not null,
                                            hash text not null
                                        );
                                        """;

    public const string UpgradeColumnsSql = """
                                           alter table migration_history
                                               add column if not exists status text not null default 'applied';
                                           alter table migration_history
                                               add column if not exists applied_at timestamptz not null default now();
                                           alter table migration_history
                                               add column if not exists error_text text null;
                                           """;

    public const string UpgradeConstraintsSql = """
                                               do $$
                                               begin
                                                   if not exists (
                                                       select 1 from pg_constraint
                                                       where conname = 'migration_history_order_number_key'
                                                         and conrelid = 'migration_history'::regclass
                                                   ) and not exists (
                                                       select 1 from pg_indexes
                                                       where tablename = 'migration_history'
                                                         and indexname = 'migration_history_order_number_key'
                                                   ) then
                                                       begin
                                                           alter table migration_history
                                                               add constraint migration_history_order_number_key unique (order_number);
                                                       exception when duplicate_table or unique_violation or duplicate_object then
                                                           null;
                                                       end;
                                                   end if;

                                                   if not exists (
                                                       select 1 from pg_constraint
                                                       where conname = 'migration_history_file_name_key'
                                                         and conrelid = 'migration_history'::regclass
                                                   ) and not exists (
                                                       select 1 from pg_indexes
                                                       where tablename = 'migration_history'
                                                         and indexname = 'migration_history_file_name_key'
                                                   ) then
                                                       begin
                                                           alter table migration_history
                                                               add constraint migration_history_file_name_key unique (file_name);
                                                       exception when duplicate_table or unique_violation or duplicate_object then
                                                           null;
                                                       end;
                                                   end if;
                                               end
                                               $$;
                                               """;

    /// <summary>
    /// Создаёт таблицу и выполняет безопасный upgrade схемы.
    /// </summary>
    public static void Ensure(NpgsqlConnection conn)
    {
        using (var cmd = new NpgsqlCommand(CreateTableSql, conn))
            cmd.ExecuteNonQuery();

        using (var cmd = new NpgsqlCommand(UpgradeColumnsSql, conn))
            cmd.ExecuteNonQuery();

        using (var cmd = new NpgsqlCommand(UpgradeConstraintsSql, conn))
            cmd.ExecuteNonQuery();
    }
}
