Nbstore migrate module for v2 to v3
NBrightBuy DNN e-commerce module v3 https://github.com/leedavi/NBrightBuy

<h2>REQUIREMENT</h2>
Dnn > V.6<br/>
Framework 4.5<br/>
NBSv3<br/>
<hr/>
<h2>INSTALLATION</h2>
On the /Installation Directory you find NBrightBuyMigrate_1.0.0_Install.zip<br/>
On DNN Host Extention install the zip file NBrightBuyMigrate_1.0.0_Install.zip<br/>
You ll find the installed plugin on NB-Store admin area on Utilities Menu<br/>
Unzip the NBrightBuyMigrate or copy all the source code under \DesktopModules\NBright\NBrightBuyMigrate<br/>

Copy all the images from \Portals\0\productimages to \Portals\0\NBStore\images
and all the docs from \Portals\0\productdocs to \Portals\0\NBStore\docs

<hr/>
<h2>NOTE</h2>

The interface has 2 button for importing. 

"Import new data migration" - This will take a v2 export data file and import it into v3.  You should use this button if it's a fresh import of data. (This is designed to loop and do the import 2 times, so all cross refs for categories and products are correctly created)

"Update existing migrated data" - If you've already imported the data file, you can use this option to redo the import if anything got missed. (Just makes things quicker, becuase in this case we don't need a double loop to link refs) 

It might be required to up the limits in the web.config "httpRuntime".  Also the limits for connection time in IIS might need to be adjusted to import and process large files.

If you can it's better to do the import on a seperate development system and then export the data from NBS using the v3 export functionality and then import into the final system.

Images can be copied directlty into the NBStore/images folder and then do a store validation on the portal from BO>Admin>Tools.  This will realign and images to the correct place on the new system.
 
<hr/>
<h2>CUSTOM FIELDS</h2>
For modify the custom field use Visual Studio 2012 and source files<br/>
Open Import.ascx.cs and go to /// CUSTOM FIELDS /// area and modify the code based on your fields<br/>
<hr/>
For any question contact me on GitHub<br/>
Fabio
