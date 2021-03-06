// SmartAPI - .Net programmatic access to RedDot servers
//  
// Copyright (C) 2013 erminas GbR
// 
// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License along with this program.
// If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using erminas.SmartAPI.CMS.Project.ContentClasses;
using erminas.SmartAPI.Exceptions;
using erminas.SmartAPI.Utils;
using erminas.SmartAPI.Utils.CachedCollections;

namespace erminas.SmartAPI.CMS.Project.Pages
{
    public interface IStartPagesValue : ILanguageDependentReadValue<IIndexedRDList<string, IPage>>, ICached
    {
        
    }

    public interface IPages
    {
        /// <summary>
        ///     All pages of the a specific language variant, indexed by page id. The list is cached by default.
        /// </summary>
        /// All Pages get loaded on first access, so this may be very slow for larger projects and you might want to use <see cref="GetByGuid"/> or <see cref="TryGetByGuid"/> instead,
        /// to only load single pages.
        IndexedRDList<int, IPage> this[string language] { get; }

        /// <summary>
        ///     All pages of the current language variant, indexed by page id. The list is cached by default.
        /// 
        /// </summary>
        ///  All Pages get loaded on first access, so this may be very slow for larger projects and you might want to use <see cref="GetByGuid"/> or <see cref="TryGetByGuid"/> instead,
        /// to only load single pages.
        IndexedRDList<int, IPage> OfCurrentLanguage { get; }

        IStartPagesValue StartPages { get; }

        /// <summary>
        ///     Get a page by its guid and its language variant. Does not load all Pages.
        /// </summary>
        /// If it does not exists, an exception gets thrown.
        IPage GetByGuid(Guid pageGuid, ILanguageVariant languageVariant);

        /// <summary>
        ///     Try to get a page by its guid and its language variant.
        /// </summary>
        /// <returns>true, if the page exists, false otherwise</returns>
        bool TryGetByGuid(Guid pageGuid, ILanguageVariant languageVariant, out IPage page);

        /// <summary>
        ///     Create a new page.
        /// </summary>
        /// <param name="cc"> Content class of the page </param>
        /// <param name="headline"> The headline, or null (default) for the default headline </param>
        /// <returns> The newly created page </returns>
        IPage Create(IContentClass cc, string headline = null);

        /// <summary>
        ///     Create a new page in the current language variant and link it.
        /// </summary>
        /// <param name="cc"> Content class of the page </param>
        /// <param name="linkGuid"> Guid of the link the page should be linked to </param>
        /// <param name="headline"> The headline, or null (default) for the default headline </param>
        /// <returns> The newly created (and linked) page </returns>
        IPage CreateAndConnect(IContentClass cc, Guid linkGuid, string headline = null);

        /// <summary>
        ///     Create an extended page search on this project.
        /// </summary>
        /// <see cref="Pages.CreateSearch" />
        IExtendedPageSearch CreateExtendedSearch();

        /// <summary>
        ///     Create a simple page search on this project.
        /// </summary>
        /// <see cref="Pages.CreateExtendedSearch" />
        IPageSearch CreateSearch();

        /// <summary>
        ///     Convenience function for simple page searches. Creates a PageSearch object, configures it through the configurator
        ///     parameter and returns the search result.
        /// </summary>
        /// <param name="configurator"> Action to configure the search </param>
        /// <returns> The search results </returns>
        /// <example>
        ///     The following code searches for all pages with headline "test":
        ///     <code>var results = project.SearchForPages(search => search.Headline="test");</code>
        /// </example>
        IEnumerable<IPage> Search(Action<IPageSearch> configurator = null);

        /// <summary>
        ///     Convenience funtion for extended page searches. Creates a new PageSearchExtended object which gets configured
        ///     through the configurator parameter and returns the result of the search.
        /// </summary>
        /// <param name="configurator"> An action to configure the search </param>
        /// <returns> The search results </returns>
        /// <example>
        ///     The following code searches for all pages saved as draft by the current user:
        ///     <code>
        /// <pre>
        ///             var results = project.SearchForPagesExtended(
        ///             search => search.AddPredicate(
        ///             new PageStatusPredicate(PageStatusPredicate.PageStatusType.SavedAsDraft,
        ///             PageStatusPredicate.UserType.CurrentUser)
        ///             )
        ///             );
        ///         </pre>
        ///      </code>
        /// </example>
        List<ResultGroup> SearchExtended(Action<IExtendedPageSearch> configurator = null);
    }

    public class Pages : IPages
    {
        private readonly Dictionary<string, IndexedRDList<int, IPage>> _pagesByLanguage =
            new Dictionary<string, IndexedRDList<int, IPage>>();

        private readonly IProject _project;
        private readonly IStartPagesValue _startPage;

        public Pages(IProject project)
        {
            _project = project;
            _startPage = new StartPagesValue(project);
        }

        public IStartPagesValue StartPages
        {
            get { return _startPage; }
        }

        public IPage GetByGuid(Guid pageGuid, ILanguageVariant languageVariant)
        {
            return new Page(_project, pageGuid, languageVariant).Refreshed();
        }

        public bool TryGetByGuid(Guid pageGuid, ILanguageVariant languageVariant, out IPage page)
        {
            try
            {
                page = GetByGuid(pageGuid, languageVariant);
                return true;
            }
            catch (SmartAPIException)
            {
                page = null;
                return false;
            }
        }

        /// <summary>
        ///     Create a new page.
        /// </summary>
        /// <param name="cc"> Content class of the page </param>
        /// <param name="headline"> The headline, or null (default) for the default headline </param>
        /// <returns> The newly created page </returns>
        public IPage Create(IContentClass cc, string headline = null)
        {
            XmlDocument xmlDoc = _project.ExecuteRQL(PageCreationString(cc, headline));
            return CreatePageFromCreationReply(xmlDoc);
        }

        /// <summary>
        ///     Create a new page in the current language variant and link it.
        /// </summary>
        /// <param name="cc"> Content class of the page </param>
        /// <param name="linkGuid"> Guid of the link the page should be linked to </param>
        /// <param name="headline"> The headline, or null (default) for the default headline </param>
        /// <returns> The newly created (and linked) page </returns>
        public IPage CreateAndConnect(IContentClass cc, Guid linkGuid, string headline = null)
        {
            const string CREATE_AND_LINK_PAGE = @"<LINK action=""assign"" guid=""{0}"">{1}</LINK>";
            XmlDocument xmlDoc =
                _project.ExecuteRQL(string.Format(CREATE_AND_LINK_PAGE, linkGuid.ToRQLString(), PageCreationString(cc, headline)));
            return CreatePageFromCreationReply(xmlDoc);
        }

        /// <summary>
        ///     Create an extended page search on this project.
        /// </summary>
        /// <see cref="CreateSearch" />
        public IExtendedPageSearch CreateExtendedSearch()
        {
            return new ExtendedPageSearch(_project);
        }

        /// <summary>
        ///     Create a simple page search on this project.
        /// </summary>
        /// <see cref="CreateExtendedSearch" />
        public IPageSearch CreateSearch()
        {
            return new PageSearch(_project);
        }

        /// <summary>
        ///     All pages of the a specific language variant, indexed by page id. The list is cached by default.
        /// </summary>
        public IndexedRDList<int, IPage> this[string language]
        {
            get { return GetPagesForLanguageVariant(language); }
        }

        /// <summary>
        ///     All pages of the current language variant, indexed by page id. The list is cached by default.
        /// </summary>
        public IndexedRDList<int, IPage> OfCurrentLanguage
        {
            get { return GetPagesForLanguageVariant(_project.LanguageVariants.Current.Abbreviation); }
        }

        /// <summary>
        ///     Convenience function for simple page searches. Creates a PageSearch object, configures it through the configurator
        ///     parameter and returns the search result.
        /// </summary>
        /// <param name="configurator"> Action to configure the search </param>
        /// <returns> The search results </returns>
        /// <example>
        ///     The following code searches for all pages with headline "test":
        ///     <code>var results = project.SearchForPages(search => search.Headline="test");</code>
        /// </example>
        public IEnumerable<IPage> Search(Action<IPageSearch> configurator = null)
        {
            var search = new PageSearch(_project);
            if (configurator != null)
            {
                configurator(search);
            }

            return search.Execute();
        }

        /// <summary>
        ///     Convenience funtion for extended page searches. Creates a new PageSearchExtended object which gets configured
        ///     through the configurator parameter and returns the result of the search.
        /// </summary>
        /// <param name="configurator"> An action to configure the search </param>
        /// <returns> The search results </returns>
        /// <example>
        ///     The following code searches for all pages saved as draft by the current user:
        ///     <code>
        /// <pre>
        ///             var results = project.SearchForPagesExtended(
        ///             search => search.AddPredicate(
        ///             new PageStatusPredicate(PageStatusPredicate.PageStatusType.SavedAsDraft,
        ///             PageStatusPredicate.UserType.CurrentUser)
        ///             )
        ///             );
        ///         </pre>
        ///      </code>
        /// </example>
        public List<ResultGroup> SearchExtended(Action<IExtendedPageSearch> configurator = null)
        {
            var search = new ExtendedPageSearch(_project);
            if (configurator != null)
            {
                configurator(search);
            }

            return search.Execute();
        }

        private IPage CreatePageFromCreationReply(XmlDocument xmlDoc)
        {
            try
            {
                var pageItem = (XmlElement) xmlDoc.GetElementsByTagName("PAGE")[0];
                return new Page(_project, pageItem);
            }
            catch (Exception e)
            {
                throw new SmartAPIException(_project.Session.ServerLogin, "Could not create page", e);
            }
        }

        private List<IPage> GetPages()
        {
            const string LIST_PAGES = @"<PROJECT><PAGES action=""list""/></PROJECT>";
            XmlDocument xmlDoc = _project.ExecuteRQL(LIST_PAGES);
            return (from XmlElement curPage in xmlDoc.GetElementsByTagName("PAGE")
                    select (IPage) new Page(_project, curPage.GetGuid(), _project.LanguageVariants.Current)
                                   {
                                       InitialHeadlineValue = curPage.GetAttributeValue("headline"),
                                       Id = curPage.GetIntAttributeValue("id")
                                           .GetValueOrDefault()
                                   }).ToList();
        }

        private IndexedRDList<int, IPage> GetPagesForLanguageVariant(string language)
        {
            ILanguageVariant languageVariant = _project.LanguageVariants[language];
            using (new LanguageContext(languageVariant))
            {
                return _pagesByLanguage.GetOrAdd(
                                                 language,
                                                 () => new IndexedRDList<int, IPage>(
                                                           () =>
                                                           {
                                                               using (new LanguageContext(languageVariant))
                                                               {
                                                                   return GetPages();
                                                               }
                                                           },
                                                           x => x.Id,
                                                           Caching.Enabled));
            }
        }

        private static string PageCreationString(IContentClass cc, string headline = null)
        {
            const string PAGE_CREATION_STRING = @"<PAGE action=""addnew"" templateguid=""{0}"" {1}/>";

            string headlineString = headline == null ? "" : string.Format(@"headline=""{0}""", HttpUtility.HtmlEncode(headline));
            return string.Format(PAGE_CREATION_STRING, cc.Guid.ToRQLString(), headlineString);
        }

     
    }

    internal class StartPagesValue : IStartPagesValue
    {
        private readonly IProject _project;
        private readonly Dictionary<ILanguageVariant, IIndexedRDList<string, IPage>> _startPagesByLanguageVariant = new Dictionary<ILanguageVariant, IIndexedRDList<string, IPage>>();
        private Guid[] _startPageGuids;

        public StartPagesValue(IProject project)
        {
            _project = project;
        }

        public IPartialRedDotProjectObject Parent
        {
            get
            {
                throw new InvalidOperationException("StartPage has no parent");
            }
        }

        public IIndexedRDList<string, IPage> ForCurrentLanguage
        {
            get { return ((ILanguageDependentReadValue < IIndexedRDList<string, IPage>>)(this))[_project.LanguageVariants.Current]; }
        }

        public IIndexedRDList<string, IPage> ForMainLanguage
        {
            get { return ((ILanguageDependentReadValue<IIndexedRDList<string, IPage>>)(this))[_project.LanguageVariants.Main]; }
        }

        IIndexedRDList<string, IPage> ILanguageDependentReadValue<IIndexedRDList<string, IPage>>.this[ILanguageVariant languageVariant]
        {
            get
            {

                return _startPagesByLanguageVariant.GetOrAdd(languageVariant, ()=>GetStartPages(languageVariant));//startPageGuids == null ? new IndexedRDList<string, IPage>(()=>new List<IPage>(), x=>x.Headline, Caching.Enabled) : _startPagesByLanguageVariant.GetOrAdd(languageVariant, ()=>GetStartPages(languageVariant));
            }
        }

        IIndexedRDList<string, IPage> ILanguageDependentReadValue<IIndexedRDList<string, IPage>>.this[string languageAbbreviation]
        {
            get { return ((ILanguageDependentReadValue<IIndexedRDList<string, IPage>>)(this))[_project.LanguageVariants[languageAbbreviation]]; }
        }

        private IIndexedRDList<string, IPage> GetStartPages(ILanguageVariant languageVariant)
        {
            return new IndexedRDList<string, IPage>(()=>Refreshed(languageVariant), x=>x.Headline, Caching.Enabled);
        }

        private List<IPage> Refreshed(ILanguageVariant languageVariant)
        {
            Refresh();
            return _startPageGuids.Select(x => _project.Pages.GetByGuid(x, languageVariant)).ToList();
        }

        private void EnsureInitialization()
        {
            if (_startPageGuids == null)
            {
                const string LOAD_DINGS = @"<LINK guid=""{0}""><PAGES action=""list"" /></LINK>";
                var doc = _project.ExecuteRQL(LOAD_DINGS.RQLFormat("00000000000000000000000000000001"));

                var pageElements = doc.GetElementsByTagName("PAGE");
                _startPageGuids = pageElements.Cast<XmlElement>()
                    .Select(XmlUtil.GetGuid)
                    .ToArray();
            }
        }

        public void InvalidateCache()
        {
            _startPageGuids = null;
            _startPagesByLanguageVariant.Clear();
        }

        public void Refresh()
        {
            InvalidateCache();
            EnsureInitialization();
        }
    }
}
