﻿using FoxIDs.Infrastructure;
using FoxIDs.Models;
using FoxIDs.Models.Config;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;

namespace FoxIDs.Repository
{
    public class MongoDbRepositoryClient
    {
        private readonly TelemetryLogger logger;
        private readonly Settings settings;
        private readonly IMongoClient mongoClient;

        public MongoDbRepositoryClient(TelemetryLogger logger, Settings settings, IMongoClient mongoClient)
        {
            this.logger = logger;
            this.settings = settings;
            this.mongoClient = mongoClient;
            Init();
        }

        private void Init()
        {
            var pack = new ConventionPack
            {
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true),
                new MongoDbJsonPropertyConvention()
            };
            ConventionRegistry.Register(nameof(MongoDbRepositoryClient), pack, t => true);

            var database = mongoClient.GetDatabase(settings.MongoDb.DatabaseName);

            _ = InitCollection<DataDocument>(database, settings.MongoDb.TenantsCollectionName);
            InitTtlCollection<DataTtlDocument>(database, settings.MongoDb.TtlTenantsCollectionName);
            _ = InitCollection<DataDocument>(database, settings.MongoDb.CacheCollectionName);
            InitTtlCollection<DataTtlDocument>(database, settings.MongoDb.TtlCacheCollectionName);
        }

        private IMongoCollection<T> InitCollection<T>(IMongoDatabase database, string name) where T : DataDocument
        {
            database.CreateCollection(name);

            var collection = database.GetCollection<T>(name);
            collection.Indexes.CreateOne(new CreateIndexModel<T>(keys: Builders<T>.IndexKeys.Ascending(f => f.PartitionId)));
            collection.Indexes.CreateOne(new CreateIndexModel<T>(keys: Builders<T>.IndexKeys.Ascending(f => f.DataType)));
            return collection;
        }

        private void InitTtlCollection<T>(IMongoDatabase database, string name) where T : DataTtlDocument
        {
            var collection = InitCollection<T>(database, name);
            collection.Indexes.CreateOne(new CreateIndexModel<T>(keys: Builders<T>.IndexKeys.Ascending(f => f.ExpireAt),
                options: new CreateIndexOptions
                {
                    ExpireAfter = TimeSpan.FromSeconds(0),
                    Name = $"{name}ExpireAtIndex"
                }));
        }

        public IMongoCollection<T> GetTenantsCollection<T>(T item = default)
        {
            if (IsTtlDocument(item))
            {
                return GetCollection<T>(settings.MongoDb.TtlTenantsCollectionName);
            }
            else
            {
                return GetCollection<T>(settings.MongoDb.TenantsCollectionName);
            }
        }

        public IMongoCollection<T> GetCacheCollection<T>(T item = default)
        {
            if (IsTtlDocument(item))
            {
                return GetCollection<T>(settings.MongoDb.TtlCacheCollectionName);
            }
            else
            {
                return GetCollection<T>(settings.MongoDb.CacheCollectionName);
            }
        }

        private static bool IsTtlDocument<T>(T item)
        {
            if (item != null)
            {
                return item.GetType().GetInterface(nameof(IDataTtlDocument)) != null;
            }
            else
            {
                return typeof(T).GetInterface(nameof(IDataTtlDocument)) != null;
            }
        }

        private IMongoCollection<T> GetCollection<T>(string name)
        {
            var database = mongoClient.GetDatabase(settings.MongoDb.DatabaseName);
            return database.GetCollection<T>(name);
        }
    }
}
