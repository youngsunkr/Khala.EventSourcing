﻿namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlMementoStore : IMementoStore
    {
        private readonly Func<IMementoStoreDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;

        public SqlMementoStore(
            Func<IMementoStoreDbContext> dbContextFactory,
            IMessageSerializer serializer)
        {
            if (dbContextFactory == null)
            {
                throw new ArgumentNullException(nameof(dbContextFactory));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            _dbContextFactory = dbContextFactory;
            _serializer = serializer;
        }

        public Task Save<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            if (memento == null)
            {
                throw new ArgumentNullException(nameof(memento));
            }

            return SaveMemento(sourceId, memento, cancellationToken);
        }

        private async Task SaveMemento(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
        {
            using (IMementoStoreDbContext context = _dbContextFactory.Invoke())
            {
                Memento entity = await context
                    .Mementoes
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync();

                if (entity == null)
                {
                    entity = new Memento { AggregateId = sourceId };
                    context.Mementoes.Add(entity);
                }

                entity.MementoJson = _serializer.Serialize(memento);

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public Task<IMemento> Find<T>(
            Guid sourceId, CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            throw new NotImplementedException();
        }

        public Task Delete<T>(
            Guid sourceId, CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            throw new NotImplementedException();
        }
    }
}