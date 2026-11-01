STYLEMART - ASP.NET CORE 8 MVC (Flipkart-style shopping flow)

FIRST-TIME SETUP (run in the folder that contains StyleMart.csproj)
1. dotnet ef database drop -f          (old database has a different schema; this resets it. Skip if the DB does not exist)
2. dotnet ef migrations add FlipkartFlow
3. dotnet ef database update
4. dotnet run

If "dotnet ef" is missing:  dotnet tool install --global dotnet-ef
Connection string: appsettings.json -> ConnectionStrings:DefaultConnection

LOGIN
Admin : /Account/Login or /Admin/Login   user: admin (or admin@stylemart.com)   password: Admin@123
        (change it in appsettings.json -> AdminSeed before first run)
Customers: use "Sign up" in the top menu.

FLOW
Home (categories + products) -> Product (colour/size, Add to Cart, Buy Now) -> Cart (+/-)
-> Login (guest cart is merged into the DB cart) -> Address -> Payment (COD) -> Order success (Order ID)
-> My Orders (view / cancel).

ADMIN (role = Admin)
Dashboard, Products (add/edit/hide/show, image upload), Categories, Variants (colour/size/stock), Orders (status updates).


NEW IN THIS VERSION
- Add to Cart / Buy Now buttons are always clickable. If colour/size is missing, the page tells the shopper what to pick (no more dead disabled button).
- Wishlist: heart on every product card and product page. Works for guests (kept in session, merged into the account on login) and logged-in users (saved in DB). Page: /Wishlist
- New menu: Home, Shop All, Categories (auto from DB), Women, Men, Help, Wishlist, Cart, My Orders, Admin links (admin only).
- New footer: trust strip, shop/account/company links, contact, social icons (replace the "#" links with your real profiles).
- Pages: /Info/About, /Info/Help, /Info/Contact (edit the text and support@stylemart.com as you like).

IMPORTANT: a new table (WishlistItems) was added, so run:
    dotnet ef migrations add AddWishlist
    dotnet ef database update
(If you have not created any migration yet, use the FIRST-TIME SETUP steps above instead.)
