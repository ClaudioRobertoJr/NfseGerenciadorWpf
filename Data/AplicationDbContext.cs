using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NfseGerenciador.Models;
using Microsoft.Extensions.Configuration;


public class ApplicationDbContext : DbContext
    {

        private readonly IConfiguration _configuration;
        public DbSet<Nfse> Nfses { get; set; }
        public DbSet<Pagamento> Pagamentos { get; set; }
        public DbSet<EmpresaClassificada> EmpresasClassificadas { get; set; }
        public DbSet<NfseParcela> NfseParcelas { get; set; }

        public ApplicationDbContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            optionsBuilder.UseSqlServer(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configura o relacionamento entre Nfse e Pagamento
            modelBuilder.Entity<Nfse>()
                .HasOne(n => n.Pagamento) // Uma NF-e tem um Pagamento
                .WithMany() // Um Pagamento pode estar em muitas NF-e (se preferir um relacionamento 1:1, altere isso)
                .HasForeignKey(n => n.PagamentoId)
                .IsRequired(false); // O pagamento não é obrigatório inicialmente (pode ser nulo)

            // Popu lar Tipos de Recebimento
            modelBuilder.Entity<Pagamento>().HasData(
                new Pagamento { Id = 1, TipoRecebimento = "Dinheiro", EhAVista = true, NumeroParcelas = 1 },
                new Pagamento { Id = 2, TipoRecebimento = "Pix", EhAVista = true, NumeroParcelas = 1 },
                new Pagamento { Id = 3, TipoRecebimento = "Cartão de Crédito", EhAVista = false, NumeroParcelas = null },
                new Pagamento { Id = 4, TipoRecebimento = "Cartão de Débito", EhAVista = true, NumeroParcelas = 1 },
                new Pagamento { Id = 5, TipoRecebimento = "Cheque", EhAVista = false, NumeroParcelas = null },
                new Pagamento { Id = 6, TipoRecebimento = "Plano de Saúde", EhAVista = true, NumeroParcelas = 1 },
                new Pagamento { Id = 7, TipoRecebimento = "Convênio", EhAVista = true, NumeroParcelas = 1 }
            );
        modelBuilder.Entity<NfseParcela>(entity =>
        {
            entity.HasOne(p => p.Nfse)                // Uma NfseParcela pertence a uma Nfse
                  .WithMany(n => n.Parcelas)          // Uma Nfse (representada por 'n') tem muitas Parcelas (a coleção 'n.Parcelas')
                  .HasForeignKey(p => p.NfseId)       // A chave estrangeira em NfseParcela é NfseId
                  .OnDelete(DeleteBehavior.Cascade);  // Se uma Nfse for deletada, suas parcelas também serão.
        });
        modelBuilder.Entity<EmpresaClassificada>()
            .HasIndex(e => e.Cnpj)
            .IsUnique();
    }
    }

