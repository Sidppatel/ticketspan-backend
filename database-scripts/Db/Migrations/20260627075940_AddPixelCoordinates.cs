using db.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Db.Migrations
{
    /// <inheritdoc />
    public partial class AddPixelCoordinates : Migration
    {
        // Floor plan moves from a fixed integer grid to free pixel coordinates.
        // Columns are RENAMED + retyped (numeric(10,2)) so existing rows survive,
        // rather than dropped/re-added. SQL procs + views are reinstalled to match.
        private static void RenameToPixel(MigrationBuilder mb, string table,
            string oldPos1, string oldPos2, string oldSize1, string oldSize2,
            string newPos1, string newPos2, string newSize1, string newSize2,
            bool nullableSize, decimal sizeDefault)
        {
            mb.RenameColumn(name: oldPos1, table: table, newName: newPos1);
            mb.RenameColumn(name: oldPos2, table: table, newName: newPos2);
            mb.RenameColumn(name: oldSize1, table: table, newName: newSize1);
            mb.RenameColumn(name: oldSize2, table: table, newName: newSize2);

            mb.AlterColumn<decimal>(name: newPos1, table: table, type: "numeric(10,2)",
                nullable: false, defaultValue: 0m, oldClrType: typeof(int), oldType: "integer");
            mb.AlterColumn<decimal>(name: newPos2, table: table, type: "numeric(10,2)",
                nullable: false, defaultValue: 0m, oldClrType: typeof(int), oldType: "integer");
            mb.AlterColumn<decimal>(name: newSize1, table: table, type: "numeric(10,2)",
                nullable: nullableSize, defaultValue: nullableSize ? (decimal?)null : sizeDefault,
                oldClrType: typeof(int), oldType: "integer");
            mb.AlterColumn<decimal>(name: newSize2, table: table, type: "numeric(10,2)",
                nullable: nullableSize, defaultValue: nullableSize ? (decimal?)null : sizeDefault,
                oldClrType: typeof(int), oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 0. Drop views that bind to the grid columns; the reinstall at the end
            //    recreates the full views folder against the new pixel columns.
            //    (Postgres blocks altering/dropping a column a view depends on.)
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_tables CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_event_tables_summary CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_events CASCADE;");

            // 1. Drop the grid unique index and grid check constraints first.
            migrationBuilder.DropIndex(name: "ix_tables_events_id_grid_row_grid_col", table: "tables");
            migrationBuilder.DropCheckConstraint(name: "CK_tables_GridRow", table: "tables");
            migrationBuilder.DropCheckConstraint(name: "CK_tables_GridCol", table: "tables");
            migrationBuilder.DropCheckConstraint(name: "CK_layout_objects_GridRow", table: "layout_objects");
            migrationBuilder.DropCheckConstraint(name: "CK_layout_objects_GridCol", table: "layout_objects");
            migrationBuilder.DropCheckConstraint(name: "CK_floor_plan_templates_Grid", table: "floor_plan_templates");
            migrationBuilder.DropCheckConstraint(name: "CK_events_GridDimensions", table: "events");

            // 2. Drop event-level canvas bounds (free, unbounded canvas now).
            migrationBuilder.DropColumn(name: "grid_rows", table: "events");
            migrationBuilder.DropColumn(name: "grid_cols", table: "events");
            migrationBuilder.DropColumn(name: "grid_rows", table: "floor_plan_templates");
            migrationBuilder.DropColumn(name: "grid_cols", table: "floor_plan_templates");

            // 3. Rename + retype position/size columns (data preserved).
            RenameToPixel(migrationBuilder, "tables",
                "grid_row", "grid_col", "row_span", "col_span",
                "pos_x", "pos_y", "width", "height", nullableSize: false, sizeDefault: 80m);
            RenameToPixel(migrationBuilder, "layout_objects",
                "grid_row", "grid_col", "row_span", "col_span",
                "pos_x", "pos_y", "width", "height", nullableSize: false, sizeDefault: 80m);
            RenameToPixel(migrationBuilder, "floor_plan_template_tables",
                "grid_row", "grid_col", "row_span", "col_span",
                "pos_x", "pos_y", "width", "height", nullableSize: false, sizeDefault: 80m);
            RenameToPixel(migrationBuilder, "floor_plan_template_objects",
                "grid_row", "grid_col", "row_span", "col_span",
                "pos_x", "pos_y", "width", "height", nullableSize: false, sizeDefault: 80m);

            // event_tables / table_templates only carry a default size (no position).
            migrationBuilder.RenameColumn(name: "row_span", table: "event_tables", newName: "default_width");
            migrationBuilder.RenameColumn(name: "col_span", table: "event_tables", newName: "default_height");
            migrationBuilder.AlterColumn<decimal>(name: "default_width", table: "event_tables",
                type: "numeric(10,2)", nullable: true, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<decimal>(name: "default_height", table: "event_tables",
                type: "numeric(10,2)", nullable: true, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.RenameColumn(name: "default_row_span", table: "table_templates", newName: "default_width");
            migrationBuilder.RenameColumn(name: "default_col_span", table: "table_templates", newName: "default_height");
            migrationBuilder.AlterColumn<decimal>(name: "default_width", table: "table_templates",
                type: "numeric(10,2)", nullable: false, defaultValue: 80m, oldClrType: typeof(int), oldType: "integer");
            migrationBuilder.AlterColumn<decimal>(name: "default_height", table: "table_templates",
                type: "numeric(10,2)", nullable: false, defaultValue: 80m, oldClrType: typeof(int), oldType: "integer");

            // 4. New non-negative position checks.
            migrationBuilder.AddCheckConstraint(name: "CK_tables_PosX", table: "tables", sql: "pos_x >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_tables_PosY", table: "tables", sql: "pos_y >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_layout_objects_PosX", table: "layout_objects", sql: "pos_x >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_layout_objects_PosY", table: "layout_objects", sql: "pos_y >= 0");

            // 5. Reinstall SQL artifacts (procs + views now reference the new columns).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS sp_update_event_grid(uuid, int, int);");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.views");
            MigrationSqlLoader.LoadAll(migrationBuilder, "Sql.stored_procedures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema-only reversal. SQL procs/views are NOT reverted (the pixel-era
            // .sql files are the only ones in the assembly); re-run a prior migration's
            // artifacts manually if a full rollback is ever needed.
            migrationBuilder.DropCheckConstraint(name: "CK_tables_PosX", table: "tables");
            migrationBuilder.DropCheckConstraint(name: "CK_tables_PosY", table: "tables");
            migrationBuilder.DropCheckConstraint(name: "CK_layout_objects_PosX", table: "layout_objects");
            migrationBuilder.DropCheckConstraint(name: "CK_layout_objects_PosY", table: "layout_objects");

            foreach (var table in new[] { "tables", "layout_objects", "floor_plan_template_tables", "floor_plan_template_objects" })
            {
                migrationBuilder.AlterColumn<int>(name: "pos_x", table: table, type: "integer",
                    nullable: false, defaultValue: 0, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
                migrationBuilder.AlterColumn<int>(name: "pos_y", table: table, type: "integer",
                    nullable: false, defaultValue: 0, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
                migrationBuilder.AlterColumn<int>(name: "width", table: table, type: "integer",
                    nullable: false, defaultValue: 1, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
                migrationBuilder.AlterColumn<int>(name: "height", table: table, type: "integer",
                    nullable: false, defaultValue: 1, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
                migrationBuilder.RenameColumn(name: "pos_x", table: table, newName: "grid_row");
                migrationBuilder.RenameColumn(name: "pos_y", table: table, newName: "grid_col");
                migrationBuilder.RenameColumn(name: "width", table: table, newName: "row_span");
                migrationBuilder.RenameColumn(name: "height", table: table, newName: "col_span");
            }

            migrationBuilder.AlterColumn<int>(name: "default_width", table: "event_tables", type: "integer",
                nullable: true, oldClrType: typeof(decimal), oldType: "numeric(10,2)", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "default_height", table: "event_tables", type: "integer",
                nullable: true, oldClrType: typeof(decimal), oldType: "numeric(10,2)", oldNullable: true);
            migrationBuilder.RenameColumn(name: "default_width", table: "event_tables", newName: "row_span");
            migrationBuilder.RenameColumn(name: "default_height", table: "event_tables", newName: "col_span");

            migrationBuilder.AlterColumn<int>(name: "default_width", table: "table_templates", type: "integer",
                nullable: false, defaultValue: 1, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
            migrationBuilder.AlterColumn<int>(name: "default_height", table: "table_templates", type: "integer",
                nullable: false, defaultValue: 1, oldClrType: typeof(decimal), oldType: "numeric(10,2)");
            migrationBuilder.RenameColumn(name: "default_width", table: "table_templates", newName: "default_row_span");
            migrationBuilder.RenameColumn(name: "default_height", table: "table_templates", newName: "default_col_span");

            migrationBuilder.AddColumn<int>(name: "grid_rows", table: "events", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "grid_cols", table: "events", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "grid_rows", table: "floor_plan_templates", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "grid_cols", table: "floor_plan_templates", type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.CreateIndex(name: "ix_tables_events_id_grid_row_grid_col", table: "tables",
                columns: new[] { "events_id", "grid_row", "grid_col" }, unique: true);
            migrationBuilder.AddCheckConstraint(name: "CK_tables_GridRow", table: "tables", sql: "grid_row >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_tables_GridCol", table: "tables", sql: "grid_col >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_layout_objects_GridRow", table: "layout_objects", sql: "grid_row >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_layout_objects_GridCol", table: "layout_objects", sql: "grid_col >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_floor_plan_templates_Grid", table: "floor_plan_templates",
                sql: "(grid_rows IS NULL OR grid_rows > 0) AND (grid_cols IS NULL OR grid_cols > 0)");
            migrationBuilder.AddCheckConstraint(name: "CK_events_GridDimensions", table: "events",
                sql: "(grid_rows IS NULL OR grid_rows > 0) AND (grid_cols IS NULL OR grid_cols > 0)");
        }
    }
}
