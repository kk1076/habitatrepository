﻿namespace Sitecore.Foundation.Multisite.Tests
{
  using System;
  using System.Collections.Generic;
  using System.Configuration;
  using System.Linq;
  using System.Xml;
  using FluentAssertions;
  using Sitecore.Collections;
  using Sitecore.Data;
  using Sitecore.FakeDb;
  using Sitecore.FakeDb.Sites;
  using Sitecore.Foundation.Multisite.Providers;
  using Sitecore.Foundation.Multisite.Tests.Extensions;
  using Sitecore.Sites;
  using Sitecore.Web;
  using Xunit;

  public class SiteDefinitionProviderTests
  {
    [Theory]
    [AutoDbData]
    public void SiteDefinitions_InvalidSiteRoot_ShouldReturnEmpty(Db db, string siteName, DbItem siteRoot, string hostName)
    {
      db.Add(siteRoot);

      var siteSettings = new StringDictionary()
                         {
                           {"name", siteName},
                           {"rootPath", siteRoot.FullPath},
                           {"targetHostName", hostName},
                           {"database", db.Database.Name},
                         };
      var site = new SiteInfo(siteSettings);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new SiteInfo[] { site });

      provider.SiteDefinitions.Should().BeEmpty();
    }


    [Theory]
    [AutoDbData]
    public void GetContextSiteDefinition_ContextItemOutsideHierarchy_ShouldReturnIsCurrentSiteDefinition(Db db, string siteName, string hostName, DbItem contextItem)
    {
      var siteRoot = new DbItem("siteRoot", ID.NewID, Templates.Site.ID);
      db.Add(siteRoot);
      db.Add(contextItem);

      var siteSettings = new StringDictionary()
                         {
                           {"name", siteName},
                           {"rootPath", siteRoot.FullPath},
                           {"targetHostName", hostName},
                           {"database", db.Database.Name}
                         };

      var site = new SiteInfo(siteSettings);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new SiteInfo[] { site });

      var currentContext = new SiteContext(site);
      using (new SiteContextSwitcher(currentContext))
      {
        provider.GetContextSiteDefinition(db.GetItem(contextItem.ID)).IsCurrent.ShouldBeEquivalentTo(true);
        provider.GetContextSiteDefinition(db.GetItem(contextItem.ID)).Name.ShouldBeEquivalentTo(site.Name);
      }
    }

    [Theory]
    [AutoDbData]
    public void SiteDefinitions_HostnameSetToInvalidHost_ShouldThrowConfigurationError(Db db, string siteName, DbItem rootItem)
    {
      var currentSite = SetupSite(db, siteName, rootItem, null, "*.test.com");
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new[] { currentSite });

      var context = new SiteContext(currentSite);
      using (new SiteContextSwitcher(context))
      {
        Action a = () => { var siteDefinitions = provider.SiteDefinitions; };
        a.ShouldThrow<ConfigurationErrorsException>();
      }
    }

    [Theory]
    [AutoDbData]
    public void SiteDefinitions_TargetHostnameNotSet_ShouldReturnHostName(Db db, string siteName, DbItem rootItem)
    {
      const string siteHostName = "www.test.com";
      var currentSite = SetupSite(db, siteName, rootItem, null, siteHostName);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new[] { currentSite });

      var context = new SiteContext(currentSite);
      using (new SiteContextSwitcher(context))
      {
        var site = provider.SiteDefinitions.First();
        site.HostName.Should().BeEquivalentTo(siteHostName);
      }
    }

    [Theory]
    [AutoDbData]
    public void SiteDefinitions_TargetHostnameSet_ShouldReturnTargetHostName(Db db, string siteName, DbItem rootItem, string targetHostName)
    {
      var currentSite = SetupSite(db, siteName, rootItem, targetHostName);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new[] { currentSite });

      var context = new SiteContext(currentSite);
      using (new SiteContextSwitcher(context))
      {
        var site = provider.SiteDefinitions.First();
        site.HostName.Should().BeEquivalentTo(targetHostName);
      }
    }

    private static SiteInfo SetupSite(Db db, string siteName, DbItem rootItem, string targetHostName = null, string hostName = null)
    {
      var siteRoot = new DbItem("siteRoot", ID.NewID, Templates.Site.ID)
                     {
                       rootItem
                     };
      db.Add(siteRoot);

      var siteSettings = new StringDictionary()
                         {
                           {"name", siteName},
                           {"rootPath", siteRoot.FullPath},
                           {"targetHostName", targetHostName},
                           {"hostName", hostName},
                           {"database", db.Database.Name}
                         };
      var currentSite = new SiteInfo(siteSettings);
      return currentSite;
    }

    [Theory]
    [AutoDbData]
    public void SiteDefinitions_NoHostnameSet_ShouldThrow(Db db, string siteName, string hostName, DbItem rootItem)
    {
      var currentSite = SetupSite(db, siteName, rootItem, null, null);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new[] { currentSite });

      var context = new SiteContext(currentSite);
      using (new SiteContextSwitcher(context))
      {
        Action a = () => { var siteDefinitions = provider.SiteDefinitions; };
        a.ShouldThrow<ConfigurationErrorsException>();
      }
    }

    [Theory]
    [AutoDbData]
    public void GetContextSiteDefinition_ContextItemInsideHierarchy_ShouldReturnHierarchicalSiteDefinition(Db db, string siteName, string hostName, DbItem contextItem)
    {
      var hierarchicalSiteRoot = new DbItem("siteRoot", ID.NewID, Templates.Site.ID) { contextItem };
      db.Add(hierarchicalSiteRoot);

      var hierarchicalSiteSettings = new StringDictionary()
                         {
                           {"name", siteName},
                           {"rootPath", hierarchicalSiteRoot.FullPath},
                           {"targetHostName", hostName},
                           {"database", db.Database.Name}
                         };
      var hierarchicalSite = new SiteInfo(hierarchicalSiteSettings);

      var currentRoot = new DbItem("otherRoot", ID.NewID, Templates.Site.ID);
      db.Add(currentRoot);
      var currentSiteSettings = new StringDictionary()
                         {
                           {"name", "current"},
                           {"rootPath", currentRoot.FullPath},
                           {"targetHostName", "otherhost.com"},
                           {"database", db.Database.Name}
                         };
      var currentSite = new SiteInfo(currentSiteSettings);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new[] { hierarchicalSite, currentSite });

      var context = new SiteContext(currentSite);
      using (new SiteContextSwitcher(context))
      {
        var contextSiteDefinition = provider.GetContextSiteDefinition(db.GetItem(contextItem.ID));
        contextSiteDefinition.IsCurrent.ShouldBeEquivalentTo(false);
        contextSiteDefinition.Name.ShouldBeEquivalentTo(hierarchicalSite.Name);
      }
    }

    [Theory]
    [AutoDbData]
    public void SiteDefinitions_ValidSiteRoot_ShouldReturnSiteDefinition(Db db, string siteName, string hostName)
    {
      var siteRoot = new DbItem("siteRoot", ID.NewID, Templates.Site.ID);
      db.Add(siteRoot);

      var siteSettings = new StringDictionary()
                         {
                           {"name", siteName},
                           {"rootPath", siteRoot.FullPath},
                           {"targetHostName", hostName},
                           {"database", db.Database.Name}
                         };
      var site = new SiteInfo(siteSettings);
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new SiteInfo[] { site });

      provider.SiteDefinitions.Should().Contain(d => d.Name == siteName);
    }

    [Fact]
    [AutoDbData]
    public void SiteDefinitions_NoSites_ShouldReturnEmpty()
    {
      ISiteDefinitionsProvider provider = new SiteDefinitionsProvider(new SiteInfo[] {});

      provider.SiteDefinitions.Should().BeEmpty();
    }
  }
}