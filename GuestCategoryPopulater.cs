#region GPLv3

// 
// Copyright (C) 2012  Chris Chenery
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 

#endregion

#region Usings

using System.Collections.Generic;
using System.Linq;
using IHI.Server.Habbos;
using IHI.Server.Libraries.Cecer1.Navigator;
using NHibernate;
using NHibernate.Criterion;
using IHI.Database;
using LibNav = IHI.Server.Libraries.Cecer1.Navigator;

#endregion

namespace IHI.Server.Plugins.Cecer1.Navigator
{
    public class GuestCategoryPopulater : Plugin
    {
        public override void Start()
        {
            Navigator navPlugin = CoreManager.ServerCore.GetPluginManager().GetPlugin("NavigatorManager") as Navigator;
            navPlugin.WaitTillStarted();

            LibNav.Navigator navgator = CoreManager.ServerCore.GetNavigator();
            navgator.OnCategoryCreated += PopulateNewCategory;
            Listing.OnMoveAny += RepopulateMovedCategory;
        }

        private void PopulateNewCategory(object source, ListingEventArgs e)
        {
            Category category = source as Category;

            foreach (GuestRoomListing listing in GetGuestRoomListings(category.IdString)) // TODO: Make the maximum amount configurable.
            {
                listing.PrimaryCategory = category;
            }
            CoreManager.ServerCore.GetStandardOut().PrintDebug("Populated new category \"" + category.IdString + "\" with rooms.");
        }
        private void RepopulateMovedCategory(object source, ListingEventArgs e)
        {
            Category category = source as Category;
            if (category == null)
                return;

            // Remove all existing room listings from the category (non-recursive).
            ICollection<Listing> listingsCollection = category.GetListings();
            Listing[] listingsArray = new Listing[listingsCollection.Count];
            category.GetListings().CopyTo(listingsArray, 0);
            foreach (Listing listing in listingsArray)
            {
                if (listing is GuestRoomListing)
                    listing.PrimaryCategory = null;
            }

            foreach (GuestRoomListing listing in GetGuestRoomListings(category.IdString)) // TODO: Make the maximum amount configurable.
            {
                listing.PrimaryCategory = category;
            }
            CoreManager.ServerCore.GetStandardOut().PrintDebug("Repopulated moved category \"" + category.IdString + "\" with rooms.");
        }

        public IEnumerable<GuestRoomListing> GetGuestRoomListings(string categoryID, int maximumAmount = 30)
        {
            HabboDistributor habboDistributor = CoreManager.ServerCore.GetHabboDistributor();
            List<int> loadedRoomIDs = new List<int>();

            // TODO: Dusokay current rooms

            using (ISession db = CoreManager.ServerCore.GetDatabaseSession())
            {
                IList<Room> rooms = db.CreateCriteria<Room>()
                    .Add(Restrictions.Eq("category_id", categoryID))
                    .Add(
                        new NotExpression(
                            new InExpression("room_id", loadedRoomIDs.Cast<object>().ToArray())))
                    .AddOrder(Order.Desc("last_entry"))
                    .SetMaxResults(maximumAmount - loadedRoomIDs.Count)
                    .List<Room>();

                foreach (Room room in rooms)
                {
                    yield return new GuestRoomListing
                                     {
                                         ID = room.room_id,
                                         Name = room.name,
                                         Description = room.description,
                                         Owner = habboDistributor.GetHabbo(room.owner_id),

                                         // TODO: Other values

                                         Population = 0 // If people were inside it would already be loaded.
                                     };
                }
            }
        }
    }
}