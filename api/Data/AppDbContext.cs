using Microsoft.EntityFrameworkCore;
using NewsCalendar.Api.Models;

namespace NewsCalendar.Api.Data;

// SQL Dev owns the schema; EF only maps it (no migrations).
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSource> EventSources => Set<EventSource>();
    public DbSet<EventUpdate> EventUpdates => Set<EventUpdate>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Event>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); // API supplies ids (DB default unused)
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(x => x.TitleNormalized).HasColumnName("title_normalized").HasMaxLength(200).IsRequired();
            e.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(500);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.EventDate).HasColumnName("event_date").HasColumnType("date");
            e.Property(x => x.ExternalKey).HasColumnName("external_key").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.SearchVector).HasColumnName("search_vector").ValueGeneratedOnAddOrUpdate(); // GENERATED: never written
            e.HasMany(x => x.Sources).WithOne().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Updates).WithOne().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventSource>(e =>
        {
            e.ToTable("event_sources");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.UpdateId).HasColumnName("update_id");
            e.Property(x => x.Url).HasColumnName("url").IsRequired();
            e.Property(x => x.UrlNormalized).HasColumnName("url_normalized").IsRequired();
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.Label).HasColumnName("label");
            e.HasOne<EventUpdate>().WithMany().HasForeignKey(x => x.UpdateId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<EventUpdate>(e =>
        {
            e.ToTable("event_updates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.UpdateDate).HasColumnName("update_date").HasColumnType("date");
            e.Property(x => x.Text).HasColumnName("text").IsRequired();
            e.Property(x => x.ShowOnCalendar).HasColumnName("show_on_calendar");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}
