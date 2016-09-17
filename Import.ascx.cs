// --- Copyright (c) notice NevoWeb ---
//  Copyright (c) 2014 SARL NevoWeb.  www.nevoweb.com.
// Author: D.C.Lee and Fabio Parigi
// ------------------------------------------------------------------------
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// ------------------------------------------------------------------------
// This copyright notice may NOT be removed, obscured or modified without written consent from the author.
// --- End copyright notice --- 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml;
using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Installer.Log;
using NBrightCore.common;
using NBrightCore.render;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuy.Admin;
//using Categories = Nevoweb.DNN.NBrightBuy.Admin.Categories;
using NBrightDNN;

using Nevoweb.DNN.NBrightBuy.Base;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuyMigrate.Components;
using DataProvider = DotNetNuke.Data.DataProvider;

namespace Nevoweb.DNN.NBrightBuyMigrate
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// The ViewNBrightGen class displays the content
    /// </summary>
    /// -----------------------------------------------------------------------------
    public partial class Import : NBrightBuyAdminBase
    {


        #region Event Handlers

        private String _ctrlkey = "";
        private NBrightBuyController _modCtrl;
        private Dictionary<int, List<String>> _courseCatXrefs;
        private Dictionary<String, List<int>> _courseXrefCats;

        private LogWriter LogOutput;

        override protected void OnInit(EventArgs e)
        {
            base.OnInit(e);

            LogOutput = new LogWriter("START: " + DateTime.Now);

            try
            {
                _modCtrl = new NBrightBuyController();

                _ctrlkey = (String)HttpContext.Current.Session["nbrightbackofficectrl"];

                #region "load templates"

                var t2 = "Importbody.html";
                // Get Display Header
                var rpDataHTempl = GetTemplateData(t2);
                rpData.ItemTemplate = NBrightBuyUtils.GetGenXmlTemplate(rpDataHTempl, StoreSettings.Current.Settings(), PortalSettings.HomeDirectory);

                #endregion


            }
            catch (Exception exc)
            {
                //display the error on the template (don;t want to log it here, prefer to deal with errors directly.)
                var l = new Literal();
                l.Text = exc.ToString();
                Controls.Add(l);
            }

        }

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                base.OnLoad(e);

                if (Page.IsPostBack == false)
                {
                    PageLoad();
                }
            }
            catch (Exception exc) //Module failed to load
            {
                //display the error on the template (don;t want to log it here, prefer to deal with errors directly.)
                var l = new Literal();
                l.Text = exc.ToString();
                Controls.Add(l);
            }
        }

        private void PageLoad()
        {
            if (UserId > 0) // only logged in users can see data on this module.
            {
                // display header
                base.DoDetail(rpData, new NBrightInfo());
            }
        }

        #endregion

        #region  "Events "

        protected void CtrlItemCommand(object source, RepeaterCommandEventArgs e)
        {
            var cArg = e.CommandArgument.ToString();
            var param = new string[3];

            switch (e.CommandName.ToLower())
            {
                case "import":
                    param[0] = "";
                    var importXML = GenXmlFunctions.GetGenXml(rpData, "", StoreSettings.Current.FolderTempMapPath);
                    var nbi = new NBrightInfo(false);
                    nbi.XMLData = importXML;

                    // we're going to loop the records 2 times.
                    // This is becuase the order of import could mean the xrefitemid and parentitemid cannot be updated on first pass.
                    // doing this import 2 times ensures we get all records existing and hence we can create valid  xrefitemid and parentitemid fields.

                    for (int i = 0; i < 2; i++)
                    {
                        DoImport(nbi);
                    }

                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
                case "update":
                    param[0] = "";
                    var importXML2 = GenXmlFunctions.GetGenXml(rpData, "", StoreSettings.Current.FolderTempMapPath);
                    var nbi2 = new NBrightInfo(false);
                    nbi2.XMLData = importXML2;

                    // this is a update to existsing data, so only need run once.
                    DoImport(nbi2);

                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
                case "cancel":
                    param[0] = "";
                    Response.Redirect(NBrightBuyUtils.AdminUrl(TabId, param), true);
                    break;
            }

        }


        #endregion

        private String GetTemplateData(String templatename)
        {
            var controlMapPath = HttpContext.Current.Server.MapPath("/DesktopModules/NBright/NBrightBuyMigrate");
            var templCtrl = new NBrightCore.TemplateEngine.TemplateGetter(PortalSettings.Current.HomeDirectoryMapPath, controlMapPath, "Themes\\config", "");
            var templ = templCtrl.GetTemplateData(templatename, Utils.GetCurrentCulture());
            templ = Utils.ReplaceSettingTokens(templ, StoreSettings.Current.Settings());
            templ = Utils.ReplaceUrlTokens(templ);
            return templ;
        }

        private void DoImport(NBrightInfo nbi)
        {
             

            var fname = StoreSettings.Current.FolderTempMapPath + "\\" + nbi.GetXmlProperty("genxml/hidden/hiddatafile");
            if (System.IO.File.Exists(fname))
            {
                var xmlFile = new XmlDocument();
                try
                {
                    xmlFile.Load(fname);
                }
                catch (Exception e)
                {
                    Exceptions.LogException(e);
                    NBrightBuyUtils.SetNotfiyMessage(ModuleId, "xmlloadfail", NotifyCode.fail, ControlPath + "/App_LocalResources/Import.ascx.resx");
                    return;
                }

                // Ref old cat id , id new cat
                ////////////////////////////////////////////////////////////////////////////
                Dictionary<string, int> categoryListIDGiud = new Dictionary<string, int>();
                Dictionary<int, int> categoryListIDFather = new Dictionary<int, int>();
                ///////////////////////////////////////////////////////////////////////////

                // var custom to be delete///////////////////////////////////////////
                List<string> listProgrampower = new List<string>();
                List<string> listVoiceCoil = new List<string>();
                // var custom to be delete///////////////////////////////////////////

                var objCtrl = new NBrightBuyController();

                // get all valid languages
                var langList = DnnUtils.GetCultureCodeList(PortalId);
                foreach (var lang in langList)
                {

                    //Import Categories

                    #region "categories"

                    var nodList = xmlFile.SelectNodes("root/categories/" + lang + "/NB_Store_CategoriesInfo");
                    if (nodList != null)
                    {
                        var categoryid = -1;
                        foreach (XmlNode nod in nodList)
                        {
                            try
                            {

                                //if category Id exist replage guidKey
                                var guidKeyNod = nod.SelectSingleNode("CategoryID").InnerText;
                                if (guidKeyNod != null)
                                {

                                    var guidKey = guidKeyNod;
                                    categoryid = -1;
                                    // see if category already exists (update if so)
                                    var nbiImport = objCtrl.GetByGuidKey(PortalSettings.Current.PortalId, -1, "CATEGORY", guidKey);
                                    if (nbiImport != null) categoryid = nbiImport.ItemID;
                                    var CategoryData = new CategoryData(categoryid, lang);

                                    // clear down existing XML data
                                    var i = new NBrightInfo(true);
                                    i.PortalId = PortalSettings.Current.PortalId;
                                    CategoryData.DataRecord.XMLData = i.XMLData;
                                    CategoryData.DataLangRecord.XMLData = i.XMLData;

                                    // assign guidkey to legacy productid (guidKey)
                                    CategoryData.DataRecord.GUIDKey = guidKey;
                                    CategoryData.DataRecord.SetXmlProperty("genxml/textbox/txtcategoryref", guidKey);

                                    // do mapping of XML data 
                                    // DATA FIELDS
                                    var cD_CategoryID = nod.SelectSingleNode("CategoryID").InnerText;
                                    var cD_PortalID = nod.SelectSingleNode("PortalID").InnerText;
                                    var cD_Archived = nod.SelectSingleNode("Archived").InnerText;
                                    var cD_Hide = nod.SelectSingleNode("Hide").InnerText;
                                    var cD_CreatedByUser = nod.SelectSingleNode("CreatedByUser").InnerText;
                                    var cD_CreatedDate = nod.SelectSingleNode("CreatedDate").InnerText;
                                    var cD_ParentCategoryID = nod.SelectSingleNode("ParentCategoryID").InnerText;
                                    var cD_ListOrder = nod.SelectSingleNode("ListOrder").InnerText;
                                    var cD_Lang = nod.SelectSingleNode("Lang").InnerText;
                                    var cD_ProductCount = nod.SelectSingleNode("ProductCount").InnerText;
                                    var cD_ProductTemplate = nod.SelectSingleNode("ProductTemplate").InnerText;
                                    var cD_ListItemTemplate = nod.SelectSingleNode("ListItemTemplate").InnerText;
                                    var cD_ListAltItemTemplate = nod.SelectSingleNode("ListAltItemTemplate").InnerText;
                                    var cD_ImageURL = nod.SelectSingleNode("ImageURL").InnerText;

                                    // DATA LANG FIELDS
                                    var cL_CategoryName = nod.SelectSingleNode("CategoryName").InnerText;
                                    var cL_ParentName = nod.SelectSingleNode("ParentName").InnerText;
                                    var cL_CategoryDesc = nod.SelectSingleNode("CategoryDesc").InnerText;
                                    var cL_Message = nod.SelectSingleNode("Message").InnerText;
                                    var cL_SEOPageTitle = nod.SelectSingleNode("SEOPageTitle").InnerText;
                                    var cL_SEOName = nod.SelectSingleNode("SEOName").InnerText;
                                    var cL_MetaDescription = nod.SelectSingleNode("MetaDescription").InnerText;
                                    var cL_MetaKeywords = nod.SelectSingleNode("MetaKeywords").InnerText;

                                    // Populate DATA CATEGORY
                                    CategoryData.DataRecord.SetXmlProperty("genxml/hidden/recordsortorder", cD_ListOrder);
                                    CategoryData.DataRecord.SetXmlProperty("genxml/checkbox/chkishidden", cD_Hide);
                                    CategoryData.DataRecord.SetXmlProperty("genxml/checkbox/chkdisable", "False");
                                    CategoryData.DataRecord.SetXmlProperty("genxml/dropdownlist/ddlgrouptype", "cat");

                                    if (cD_ParentCategoryID != null && cD_ParentCategoryID != "0") CategoryData.DataRecord.SetXmlProperty("genxml/dropdownlist/ddlparentcatid", cD_ParentCategoryID);

                                    // Populate DATA CATEGORY LANG


                                    if (cL_CategoryName != null && cL_CategoryName != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtcategoryname", cL_CategoryName);
                                    if (cL_CategoryDesc != null && cL_CategoryDesc != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtcategorydesc", cL_CategoryDesc);
                                    if (cL_MetaDescription != null && cL_MetaDescription != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtmetadescription", cL_MetaDescription);
                                    if (cL_MetaKeywords != null && cL_MetaKeywords != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtmetakeywords", cL_MetaKeywords);
                                    if (cL_SEOPageTitle != null && cL_SEOPageTitle != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseopagetitle", cL_SEOPageTitle);
                                    if (cL_SEOName != null && cL_SEOName != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseoname", cL_SEOName);
                                    if (cL_Message != null && cL_Message != "") CategoryData.DataLangRecord.SetXmlProperty("genxml/edt/message", cL_Message);

                                    categoryListIDGiud.Add(CategoryData.CategoryRef, CategoryData.CategoryId);
                                    if (cD_ParentCategoryID != null && cD_ParentCategoryID != "" && cD_ParentCategoryID != "0")
                                        categoryListIDFather.Add(CategoryData.CategoryId, Convert.ToInt32(cD_ParentCategoryID));


                                    CategoryData.Save();


                                }

                            }
                            catch (Exception e)
                            {
                                var logMessage = "CATEGORY: CategoryId: " + categoryid.ToString() + " : " + e.ToString();
                                LogOutput.LogWrite(logMessage);
                            }

                        }

                    //loop on The dictionary
                    foreach (var catl in categoryListIDFather)
                        {
                            //Key
                            var tempNewID = catl.Key;
                            //Value
                            var tempOldFather = catl.Value;

                            var tempNewFather = categoryListIDGiud[tempOldFather.ToString()];

                            var CategoryData = new CategoryData(tempNewID, lang);
                            CategoryData.ParentItemId = tempNewFather;
                            CategoryData.DataRecord.SetXmlProperty("genxml/dropdownlist/ddlparentcatid", tempNewFather.ToString());
                            CategoryData.Save();

                        }

                    }

                    #endregion

                    // Import Products

                    #region "data"

                    nodList = xmlFile.SelectNodes("root/products/" + lang + "/P");
                    if (nodList != null)
                    {
                        var productid = -1;
                        var prodname = "";
                        foreach (XmlNode nod in nodList)
                        {

                            try
                            {

                                var guidKeyNod = nod.SelectSingleNode("NB_Store_ProductsInfo/ProductID");
                                if (guidKeyNod != null)
                                {
                                    var guidKey = guidKeyNod.InnerText;
                                    productid = -1;
                                    // See if e already have a product imported, if so we want to update (maybe multiple languages, or a 2nd migrate)
                                    var nbiImport = objCtrl.GetByGuidKey(PortalSettings.Current.PortalId, -1, "PRD", guidKey);
                                    if (nbiImport != null) productid = nbiImport.ItemID;
                                    var productData = new ProductData(productid, lang);

                                    productid = productData.Info.ItemID; // set productid, so we have the id if it goes wrong.
                                    prodname = productData.ProductName;

                                    // clear down existing XML data
                                    var i = new NBrightInfo(true);
                                    productData.DataRecord.XMLData = i.XMLData;
                                    productData.DataLangRecord.XMLData = i.XMLData;

                                    // assign guidkey to legacy productid (guidKey)
                                    productData.DataRecord.GUIDKey = guidKey;
                                    productData.DataRecord.SetXmlProperty("genxml/importref", guidKey);

                                    // do mapping of XML data 
                                    // DATA FIELDS
                                    var pD_ProductID = nod.SelectSingleNode("NB_Store_ProductsInfo/ProductID").InnerText;
                                    var pD_PortalID = nod.SelectSingleNode("NB_Store_ProductsInfo/PortalID").InnerText;
                                    var pD_TaxCategoryID = nod.SelectSingleNode("NB_Store_ProductsInfo/TaxCategoryID").InnerText;
                                    var pD_Featured = nod.SelectSingleNode("NB_Store_ProductsInfo/Featured").InnerText;
                                    var pD_Archived = nod.SelectSingleNode("NB_Store_ProductsInfo/Archived").InnerText;
                                    var pD_CreatedByUser = nod.SelectSingleNode("NB_Store_ProductsInfo/CreatedByUser").InnerText;
                                    var pD_CreatedDate = nod.SelectSingleNode("NB_Store_ProductsInfo/CreatedDate").InnerText;
                                    var pD_IsDeleted = nod.SelectSingleNode("NB_Store_ProductsInfo/IsDeleted").InnerText;
                                    var pD_ProductRef = nod.SelectSingleNode("NB_Store_ProductsInfo/ProductRef").InnerText;
                                    var pD_Lang = nod.SelectSingleNode("NB_Store_ProductsInfo/Lang").InnerText;
                                    var pD_Manufacturer = nod.SelectSingleNode("NB_Store_ProductsInfo/Manufacturer").InnerText;
                                    var pD_ModifiedDate = nod.SelectSingleNode("NB_Store_ProductsInfo/ModifiedDate").InnerText;
                                    var pD_IsHidden = nod.SelectSingleNode("NB_Store_ProductsInfo/IsHidden").InnerText;
                                    // DATA LANG FIELDS
                                    var pL_ProductName = nod.SelectSingleNode("NB_Store_ProductsInfo/ProductName").InnerText;
                                    var pL_Summary = nod.SelectSingleNode("NB_Store_ProductsInfo/Summary").InnerText;
                                    var pL_Description = nod.SelectSingleNode("NB_Store_ProductsInfo/Description").InnerText;
                                    var pL_SEOName = nod.SelectSingleNode("NB_Store_ProductsInfo/SEOName").InnerText;
                                    var pL_TagWords = nod.SelectSingleNode("NB_Store_ProductsInfo/TagWords").InnerText;
                                    var pL_Desc = nod.SelectSingleNode("NB_Store_ProductsInfo/Description").InnerText;

                                    if (pD_ProductRef != null) productData.DataRecord.SetXmlProperty("genxml/textbox/txtproductref", pD_ProductRef);
                                    if (pD_Manufacturer != null) productData.DataRecord.SetXmlProperty("genxml/textbox/customorder", pD_Manufacturer);

                                    // langauge main fields
                                    if (pL_ProductName != null) productData.DataLangRecord.SetXmlProperty("genxml/textbox/txtproductname", pL_ProductName);
                                    if (pL_Summary != null) productData.DataLangRecord.SetXmlProperty("genxml/textbox/txtsummary", pL_Summary);
                                    if (pL_SEOName != null) productData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseoname", pL_SEOName);
                                    if (pL_SEOName != null) productData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseopagetitle", pL_SEOName);
                                    if (pL_TagWords != null) productData.DataLangRecord.SetXmlProperty("genxml/textbox/txttagwords", pL_TagWords);

                                    //edt

                                    if (pL_Desc != null)
                                    {
                                        productData.DataLangRecord.SetXmlProperty("genxml/edt", "");
                                        productData.DataLangRecord.SetXmlProperty("genxml/edt/description", pL_Desc);
                                    }


                                    ////////////////////////////// CUSTOM FIELDS /////////////////////////////////////
                                    ////////////////////////////// CUSTOM FIELDS /////////////////////////////////////
                                    ////////////////////////////// CUSTOM FIELDS /////////////////////////////////////
                                    #region "Custom Fields"

                                    // Custom Fields the Custom fields in Nbstore are stored in XmlData xml file area.

                                    if (nod.SelectSingleNode("NB_Store_ProductsInfo/XMLData").FirstChild != null) // verify if there are custom fileds
                                    {

                                        var InputCustomFieldsXml = new XmlDocument();
                                        InputCustomFieldsXml.Load(new StringReader(nod.SelectSingleNode("NB_Store_ProductsInfo/XMLData").InnerText));

                                        var nodListCustom = InputCustomFieldsXml.SelectNodes("genxml");
                                        // tipo di scheda prodotto
                                        var ProductType = "";
                                        if (nodListCustom[0].SelectSingleNode("dropdownlist")["producttype"] != null)
                                        {
                                            ProductType = nodListCustom[0].SelectSingleNode("dropdownlist")["producttype"].InnerText;
                                        }

                                        List<String> LangCustomField = new List<string>()
                                        {
                                            "keyfeatures", "generalspecifications",
                                            "hfspecifications", "thielesmallparameters", "mountinginformations", "articlenotes",
                                            "graphicstitle1", "graphicscomment1", "graphicstitle2", "graphicscomment2"
                                        };

                                        // importazione dei campi text box
                                        var CustomListTextbox = nodListCustom[0].SelectSingleNode("textbox");
                                        foreach (XmlNode nodoTexbox in CustomListTextbox.ChildNodes)
                                        {
                                            if (nodoTexbox.InnerText != null)
                                            {
                                                if (LangCustomField.Contains(nodoTexbox.Name))
                                                {
                                                    // se il campo è in lingua
                                                    productData.DataLangRecord.SetXmlProperty("genxml/textbox/" + nodoTexbox.Name, nodoTexbox.InnerText);
                                                }
                                                else
                                                {
                                                    // se il campo non è in lingua e vado anche a effettuare trasformazioni e modifiche per alcuni campi
                                                    switch (nodoTexbox.Name)
                                                    {
                                                        case "programpower":
                                                            string progp = nodoTexbox.InnerText;
                                                            progp = progp.Replace("W", "");
                                                            progp = progp.Replace(",", ".");
                                                            progp = progp.Trim();
                                                            productData.DataRecord.SetXmlProperty("genxml/dropdownlist/" + nodoTexbox.Name, progp);
                                                            if (!listProgrampower.Contains(progp))
                                                            {
                                                                listProgrampower.Add(progp);
                                                            }
                                                            break;
                                                        case "voicecoildiameter":
                                                            string vc = nodoTexbox.InnerText;
                                                            vc = vc.Replace(",", ".");
                                                            vc = vc.Trim();
                                                            productData.DataRecord.SetXmlProperty("genxml/dropdownlist/" + nodoTexbox.Name, vc);
                                                            if (!listVoiceCoil.Contains(vc))
                                                            {
                                                                listVoiceCoil.Add(vc);
                                                            }
                                                            break;
                                                        // devo uniformare i valori stringa togliendo le virgole e mettendo dei punti
                                                        case "exitdiameter":
                                                            productData.DataRecord.SetXmlProperty("genxml/textbox/" + nodoTexbox.Name, nodoTexbox.InnerText.Replace(",", "."));
                                                            break;
                                                        case "throatdiameter":
                                                            productData.DataRecord.SetXmlProperty("genxml/textbox/" + nodoTexbox.Name, nodoTexbox.InnerText.Replace(",", "."));
                                                            break;

                                                        /////////////////////////// default per campi testo

                                                        default:
                                                            productData.DataRecord.SetXmlProperty("genxml/textbox/" + nodoTexbox.Name, nodoTexbox.InnerText);
                                                            break;
                                                    }

                                                }
                                            }
                                        }

                                        var CustomListCheckbox = nodListCustom[0].SelectSingleNode("checkbox");
                                        foreach (XmlNode nodoCheckbox in CustomListCheckbox.ChildNodes)
                                        {
                                            if (nodoCheckbox.InnerText != null)
                                            {
                                                if (LangCustomField.Contains(nodoCheckbox.Name))
                                                {
                                                    productData.DataLangRecord.SetXmlProperty("genxml/checkbox/" + nodoCheckbox.Name, nodoCheckbox.InnerText);
                                                }
                                                else
                                                {
                                                    productData.DataRecord.SetXmlProperty("genxml/checkbox/" + nodoCheckbox.Name, nodoCheckbox.InnerText);
                                                }
                                            }
                                        }

                                        var CustomListDropdownlist = nodListCustom[0].SelectSingleNode("dropdownlist");
                                        foreach (XmlNode nodoDropdownlist in CustomListDropdownlist.ChildNodes)
                                        {
                                            if (nodoDropdownlist.InnerText != null)
                                            {
                                                if (LangCustomField.Contains(nodoDropdownlist.Name))
                                                {
                                                    productData.DataLangRecord.SetXmlProperty("genxml/dropdownlist/" + nodoDropdownlist.Name, nodoDropdownlist.InnerText);
                                                }
                                                else
                                                {
                                                    productData.DataRecord.SetXmlProperty("genxml/dropdownlist/" + nodoDropdownlist.Name, nodoDropdownlist.InnerText);
                                                }
                                            }
                                        }

                                        List<String> CustomImportField = new List<string>()
                                        {
                                            "generalspecifications",
                                            "hfspecifications", "thielesmallparameters", "mountinginformations"
                                        };
                                        //add edt
                                        if (productData.DataLangRecord.XMLDoc.SelectSingleNode("genxml/edt") == null)
                                        {
                                            productData.DataLangRecord.AddSingleNode("edt", "", "genxml");
                                        }
                                        //add standard description
                                        if (pL_Description != null) productData.DataLangRecord.SetXmlProperty("genxml/edt/description", pL_Description);

                                        var CustomListEdt = nodListCustom[0].SelectSingleNode("edt");
                                        foreach (XmlNode nodoEdt in CustomListEdt.ChildNodes)
                                        {
                                            if (nodoEdt.InnerText != null)
                                            {
                                                ////////////////////////////// custom import /////////////////////////////////
                                                ////////////////////////////// custom import /////////////////////////////////
                                                ////////////////////////////// custom import /////////////////////////////////
                                                ////////////////////////////// custom import /////////////////////////////////
                                                ////////////////////////////// custom import /////////////////////////////////
                                                if (LangCustomField.Contains(nodoEdt.Name))
                                                {
// se è un campo di testo lungo e fa parte dell'elenco di quelli da modificare lacio la funzione di importazione custom
                                                    if (CustomImportField.Contains(nodoEdt.Name))
                                                    {
                                                        ImportCustom(productData, nodoEdt.Name, nodoEdt.InnerText, ProductType);
                                                    }
                                                    else
                                                    {
                                                        productData.DataLangRecord.AddSingleNode(nodoEdt.Name, "", "genxml/edt");
                                                        productData.DataLangRecord.SetXmlProperty("genxml/edt/" + nodoEdt.Name, nodoEdt.InnerText);
                                                    }
                                                }
                                                else
                                                {
                                                    productData.DataRecord.AddSingleNode(nodoEdt.Name, "", "genxml/edt");
                                                    productData.DataRecord.SetXmlProperty("genxml/edt/" + nodoEdt.Name, nodoEdt.InnerText);
                                                }
                                            }
                                        }
                                    }

                                    #endregion
                                    ////////////////////////////// END CUSTOM FIELDS /////////////////////////////////////
                                    ////////////////////////////// END CUSTOM FIELDS /////////////////////////////////////
                                    ////////////////////////////// END CUSTOM FIELDS /////////////////////////////////////                            


                                    // Models
                                    var nodListModels = nod.SelectNodes("M/NB_Store_ModelInfo");
                                    foreach (XmlNode nodMod in nodListModels)
                                    {
                                        //load single node module strucuture
                                        var ModelID = nodMod.SelectSingleNode("ModelID").InnerText;

                                        var ProductID = nodMod.SelectSingleNode("ProductID").InnerText;
                                        var ListOrder = nodMod.SelectSingleNode("ListOrder").InnerText;
                                        var UnitCost = nodMod.SelectSingleNode("UnitCost").InnerText;
                                        var Barcode = nodMod.SelectSingleNode("Barcode").InnerText;
                                        var ModelRef = nodMod.SelectSingleNode("ModelRef").InnerText;
                                        var Lang = nodMod.SelectSingleNode("Lang").InnerText;
                                        var ModelName = nodMod.SelectSingleNode("ModelName").InnerText;
                                        var QtyRemaining = nodMod.SelectSingleNode("QtyRemaining").InnerText;
                                        var QtyTrans = nodMod.SelectSingleNode("QtyTrans").InnerText;
                                        var QtyTransDate = nodMod.SelectSingleNode("QtyTransDate").InnerText;
                                        var ProductName = nodMod.SelectSingleNode("ProductName").InnerText;
                                        var PortalID = nodMod.SelectSingleNode("PortalID").InnerText;
                                        var Weight = nodMod.SelectSingleNode("Weight").InnerText;
                                        var Height = nodMod.SelectSingleNode("Height").InnerText;
                                        var Length = nodMod.SelectSingleNode("Length").InnerText;
                                        var Width = nodMod.SelectSingleNode("Width").InnerText;
                                        var Deleted = nodMod.SelectSingleNode("Deleted").InnerText;
                                        var QtyStockSet = nodMod.SelectSingleNode("QtyStockSet").InnerText;
                                        var DealerCost = nodMod.SelectSingleNode("DealerCost").InnerText;
                                        var PurchaseCost = nodMod.SelectSingleNode("PurchaseCost").InnerText;
                                        var XMLData = nodMod.SelectSingleNode("XMLData").InnerText;
                                        var Extra = nodMod.SelectSingleNode("Extra").InnerText;
                                        var DealerOnly = nodMod.SelectSingleNode("DealerOnly").InnerText;
                                        var Allow = nodMod.SelectSingleNode("Allow").InnerText;


                                        ////////////////////////////// FINE CUSTOM FIELDS /////////////////////////////////////
                                        ////////////////////////////// FINE CUSTOM FIELDS /////////////////////////////////////
                                        ////////////////////////////// FINE CUSTOM FIELDS /////////////////////////////////////



                                        //il dentro al tag model cè un genxml che identifica il modello
                                        // MODELLI CAMPI DATA
                                        ////////////////////////////// MODELS /////////////////////////////////////
                                        ////////////////////////////// MODELS /////////////////////////////////////
                                        ////////////////////////////// MODELS /////////////////////////////////////
                                        var newkey = Utils.GetUniqueKey();

                                        var strXmlModel = @"<genxml><models><genxml>
                                      <files />
                                      <hidden><modelid>" + newkey + "</modelid>" +
                                                          @"</hidden>
                                      <textbox>
                                        <availabledate datatype=""date"" />
                                        <txtqtyminstock>0</txtqtyminstock>
                                        <txtmodelref>" + ModelRef + @"</txtmodelref>
                                        <txtunitcost>" + UnitCost + @"</txtunitcost>
                                        <txtsaleprice>0.00</txtsaleprice>
                                        <txtbarcode>" + Barcode + @"</txtbarcode>
                                        <txtqtyremaining>" + QtyRemaining + @"</txtqtyremaining>
                                        <txtqtystockset>" + QtyStockSet + @"</txtqtystockset>
                                        <txtdealercost>" + DealerCost + @"</txtdealercost>
                                        <txtpurchasecost>" + PurchaseCost + @"</txtpurchasecost>
                                        <weight>" + Weight + @"</weight>
                                        <depth>" + Length + @"</depth>
                                        <width>" + Width + @"</width>
                                        <height>" + Height + @"</height>
                                        <unit />
                                        <delay />
                                      </textbox>
                                      <checkbox>
                                        <chkstockon>False</chkstockon>
                                        <chkishidden>False</chkishidden>
                                        <chkdeleted>False</chkdeleted>
                                        <chkdealeronly>False</chkdealeronly>
                                      </checkbox>
                                      <dropdownlist>
                                        <modelstatus>010</modelstatus>
                                      </dropdownlist>
                                      <checkboxlist />
                                      <radiobuttonlist />
                                    </genxml></models></genxml>";

                                        var strXmlModelLang = @"<genxml><models><genxml>
                                        <files />
                                        <hidden />
                                        <textbox>
                                            <txtmodelname>" + ModelName + "</txtmodelname>" +
                                                              "<txtextra>" + Extra + @"</txtextra>
                                        </textbox>
                                        <checkbox /><dropdownlist /><checkboxlist /><radiobuttonlist />
                                        </genxml></models></genxml>";

                                        if (productData.DataRecord.XMLDoc.SelectSingleNode("genxml/models") == null)
                                        {
                                            productData.DataRecord.AddXmlNode(strXmlModel, "genxml/models", "genxml");
                                            productData.DataLangRecord.AddXmlNode(strXmlModelLang, "genxml/models", "genxml");
                                        }
                                        else
                                        {
                                            productData.DataRecord.AddXmlNode(strXmlModel, "genxml/models/genxml", "genxml/models");
                                            productData.DataLangRecord.AddXmlNode(strXmlModelLang, "genxml/models/genxml", "genxml/models");
                                        }


                                    }

                                    ////////////////////////////// IMAGES /////////////////////////////////////
                                    ////////////////////////////// IMAGES /////////////////////////////////////
                                    ////////////////////////////// IMAGES /////////////////////////////////////
                                    ////////////////////////////// IMAGES /////////////////////////////////////
                                    // copy all the images from Portals\0\productimages to Portals\0\NBStore\images

                                    var nodListImages = nod.SelectNodes("I/NB_Store_ProductImageInfo");
                                    foreach (XmlNode nodImg in nodListImages)
                                    {
                                        var ImageID = nodImg.SelectSingleNode("ImageID").InnerText;
                                        var Hidden = nodImg.SelectSingleNode("Hidden").InnerText;
                                        var ImageUrl = nodImg.SelectSingleNode("ImageURL").InnerText;
                                        var ImagePath = nodImg.SelectSingleNode("ImagePath").InnerText;

                                        productData.AddNewImage(ImageUrl, ImagePath);
                                    }
                                    ////////////////////////////// DOCS /////////////////////////////////////
                                    ////////////////////////////// DOCS /////////////////////////////////////
                                    ////////////////////////////// DOCS /////////////////////////////////////
                                    ////////////////////////////// DOCS /////////////////////////////////////
                                    // copy all the DOCS from Portals\0\productdocs to Portals\0\NBStore\docs

                                    var nodListDocs = nod.SelectNodes("D/NB_Store_ProductDocInfo");
                                    var lp = 1;
                                    var objCtrlDoc = new NBrightBuyController();

                                    foreach (XmlNode nodDoc in nodListDocs)
                                    {
                                        var DocID = nodDoc.SelectSingleNode("DocID").InnerText;
                                        var ProductID = nodDoc.SelectSingleNode("ProductID").InnerText;
                                        var DocPath = nodDoc.SelectSingleNode("DocPath").InnerText;
                                        var ListOrder = nodDoc.SelectSingleNode("ListOrder").InnerText;
                                        var Hidden = nodDoc.SelectSingleNode("Hidden").InnerText;
                                        var FileName = nodDoc.SelectSingleNode("FileName").InnerText;
                                        var FileExt = nodDoc.SelectSingleNode("FileExt").InnerText;
                                        var Lang = nodDoc.SelectSingleNode("Lang").InnerText;
                                        var DocDesc = nodDoc.SelectSingleNode("DocDesc").InnerText;

                                        productData.AddNewDoc(DocPath, FileName);

                                        productData.DataRecord.SetXmlProperty("genxml/docs/genxml[" + lp.ToString() + "]/hidden/fileext", FileExt);
                                        DocPath = DocPath.Replace("productdocs", @"NBStore\docs");
                                        productData.DataRecord.SetXmlProperty("genxml/docs/genxml[" + lp.ToString() + "]/hidden/filepath", DocPath);

                                        objCtrlDoc.Update(productData.DataRecord);

                                        // if doesen't exisit the xml genxml strucuture inside the DataLangRecor I create it
                                        var strXml = "<genxml><docs><genxml><textbox/></genxml></docs></genxml>";
                                        if (productData.DataLangRecord.XMLDoc.SelectSingleNode("genxml/docs") == null)
                                        {
                                            productData.DataLangRecord.AddXmlNode(strXml, "genxml/docs", "genxml");
                                        }
                                        else
                                        {
                                            productData.DataLangRecord.AddXmlNode(strXml, "genxml/docs/genxml", "genxml/docs");
                                        }
                                        /////////////////////////////////////////////////
                                        productData.DataLangRecord.SetXmlProperty("genxml/docs/genxml[" + lp.ToString() + "]/textbox/txtdocdesc", DocDesc);
                                        productData.DataLangRecord.SetXmlProperty("genxml/docs/genxml[" + lp.ToString() + "]/textbox/txttitle", FileName);

                                        objCtrlDoc.Update(productData.DataLangRecord);


                                        lp += 1;
                                    }









                                    ////////////////////////////// CATEGORIES /////////////////////////////////////
                                    ////////////////////////////// CATEGORIES /////////////////////////////////////
                                    ////////////////////////////// CATEGORIES /////////////////////////////////////
                                    ////////////////////////////// CATEGORIES /////////////////////////////////////

                                    var nodListCat = nod.SelectNodes("C/NB_Store_ProductCategoryInfo");
                                    foreach (XmlNode nodCat in nodListCat)
                                    {
                                        var ProductID = nodCat.SelectSingleNode("ProductID").InnerText;
                                        var CategoryID = nodCat.SelectSingleNode("CategoryID").InnerText;

                                        if (ProductID == guidKey)
                                        {
                                            var newCategoryId = categoryListIDGiud[CategoryID];
                                            productData.AddCategory(newCategoryId);
                                        }

                                    }





                                    ////////////////////////////// SAVE PRODUCT /////////////////////////////////////
                                    productData.Save();

                                }

                            }
                            catch (Exception e)
                            {
                                var logMessage = "PRODUCTS: " + prodname +  " ProductId: " + productid.ToString() + " : " + e.ToString();
                                LogOutput.LogWrite(logMessage);
                            }

                        }
                    }

                    ////////////////////////////// RELATED PRODUCTS /////////////////////////////////////
                    //recicle on all the xml import Product and reconnect the related product
                    foreach (var lang2 in langList)
                    {
                        if (nodList != null)
                        {
                            foreach (XmlNode nod in nodList)
                            {

                                var guidKeyNod = nod.SelectSingleNode("NB_Store_ProductsInfo/ProductID");
                                if (guidKeyNod != null)
                                {

                                    var guidKey = guidKeyNod.InnerText;
                                    var productid = -1;
                                    try
                                    {
                                        // See if e already have a product imported, if so we want to update (maybe multiple languages, or a 2nd migrate)
                                        var nbiImport = objCtrl.GetByGuidKey(PortalSettings.Current.PortalId, -1, "PRD", guidKey);
                                        if (nbiImport != null) productid = nbiImport.ItemID;
                                        var productData = new ProductData(productid, lang2);

                                        ////////////////////////////// RELATED PRODUCTS /////////////////////////////////////
                                        ////////////////////////////// RELATED PRODUCTS /////////////////////////////////////
                                        var nodListRelated = nod.SelectNodes("R/NB_Store_ProductRelatedInfo");
                                        if (nodListRelated != null) //if there are related
                                        {

                                            foreach (XmlNode nodRel in nodListRelated)
                                            {
                                                // id in the related product import file
                                                var ImportRelatedProductID = nodRel.SelectSingleNode("RelatedProductID").InnerText;
                                                // extract Id of the new created product thet have the old id in the guidKey
                                                var tempID = objCtrl.GetByGuidKey(PortalSettings.Current.PortalId, -1, "PRD", ImportRelatedProductID);

                                                if (tempID != null)
                                                {
                                                    int IDRelatedprod = tempID.ItemID;
                                                    productData.AddRelatedProduct(IDRelatedprod);
                                                    productData.Save();
                                                }

                                            }
                                        }

                                    }
                                    catch (Exception e)
                                    {
                                        var logMessage = "RELATED PRODUCTS: ProductId: " + productid.ToString() + " : " + e.ToString();
                                        LogOutput.LogWrite(logMessage);
                                    }

                                }

                        }
                        }
                    }

                    LogOutput.LogWrite("END: " + DateTime.Now);

                    #endregion

                }

                NBrightBuyUtils.SetNotfiyMessage(ModuleId, "Import", NotifyCode.ok, ControlPath + "/App_LocalResources/Import.ascx.resx");

                // pezzo custom da eliminare
                listProgrampower.Sort();
                string pp = "";
                foreach (string v in listProgrampower)
                {
                    pp = pp + ";" + v;
                }

                // pezzo custom da eliminare
                listVoiceCoil.Sort();
                string cc = "";
                foreach (string v in listVoiceCoil)
                {
                    cc = cc + ";" + v;
                }

            }

        }

        public void ImportCustom(NBrightBuy.Components.ProductData p, string edtFieldName, string text, string productype)
        {
            ////////////////////////////// custom import /////////////////////////////////
            ////////////////////////////// custom import /////////////////////////////////


        string testoDecod = DecodeXmlString(text);
        //
        string[] values = testoDecod.Split(new string[] { "<tr>", "</tr>", "<td>", "</td>" }, StringSplitOptions.RemoveEmptyEntries);
        List<string> list = new List<string>(values);
        if (edtFieldName == "generalspecifications")
        {
            for (int i = 0; i < list.Count; i++)
            {
                //LFNeo;LFFer;DriverNeo;DriverFer;Coaxial;Horn;LineArray
                if (productype == "DriverNeo" || productype == "DriverFer")
                {
                    //general spec for driver importo in maniera divera le general spec se sono driver
                    if (list[i].Contains("Throat Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_throatdiameter", ExtrVlist(list, i)); }
                    if (list[i].Contains("Rated Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_ratedimpedance", ExtrVlist(list, i)); }
                    if (list[i].Contains("DC Resistance") || list[i].Contains("D.C. Resistance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_dcresistance", ExtrVlist(list, i)); }
                    if (list[i].Contains("Minimum Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_minimumimpedance", ExtrVlist(list, i)); }
                    if (list[i].Contains("Le (")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_le", ExtrVlist(list, i)); }
                    if (list[i].Contains("AES")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_aespower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Continuous Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_continuouspower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Program power") || list[i].Contains("Program Power") || list[i].Contains("Max. program power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_programpower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Sensitivity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_sensitivity", ExtrVlist(list, i)); }
                    if (list[i].Contains("Frequency Range")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_frequencyrange", ExtrVlist(list, i)); }
                    if (list[i].Contains("Min. Xover Frequency") || list[i].Contains("Min Xover Frequency") || list[i].Contains("Mininimum Xover Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_minxover", ExtrVlist(list, i)); }
                    if (list[i].Contains("Recomm. Xover Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_recomxover", ExtrVlist(list, i)); }
                    if (list[i].Contains("Diaphragm material") || list[i].Contains("Diaphragm Material")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_diaphragmmaterial", ExtrVlist(list, i)); }
                    if (list[i].Contains("Voice Coil Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_voicecoildiameter", ExtrVlist(list, i)); }
                    if (list[i].Contains("Voice Coil Winding") || list[i].Contains("Voice Coil winding")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_voicecoilwindingmaterial", ExtrVlist(list, i)); }
                    if (list[i].Contains("Magnet Material")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_magnetmaterial", ExtrVlist(list, i)); }
                    if (list[i].Contains("Flux Density")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_fluxdensity", ExtrVlist(list, i)); }
                    if (list[i].Contains("Bl Factor") || list[i].Contains("BL Factor")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_blfactor", ExtrVlist(list, i)); }
                    if (list[i].Contains("Polarity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_polarity", ExtrVlist(list, i)); }

                }
                else if (productype == "Horn" || productype == "LineArray")
                { 
                    if (list[i].Contains("Throat Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_throatdiameter", ExtrVlist(list, i)); }
                    if (list[i].Contains("Horizontal Coverage")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_horizontalcoverage", ExtrVlist(list, i)); }
                    if (list[i].Contains("Vertical Coverage")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_verticalcoverage", ExtrVlist(list, i)); }
                    if (list[i].Contains("Directivity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_directivityindex", ExtrVlist(list, i)); }
                    if (list[i].Contains("Usable Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_usablefrequencyrange", ExtrVlist(list, i)); }
                    if (list[i].Contains("Xover") || list[i].Contains("Cross")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_recomxover", ExtrVlist(list, i)); }
                    if (list[i].Contains("Sensitivity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_sensitivity", ExtrVlist(list, i)); }
                    if (list[i].Contains("Frequency Range")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_frequencyrange", ExtrVlist(list, i)); }
                    if (list[i].Contains("Material")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhorn_material", ExtrVlist(list, i)); }                
                
                }

                else
                {
                    //standard general specifications + coax
                    if (list[i].Contains("Nominal Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_nominaldiameter", ExtrVlist(list, i)); }
                    if (list[i].Contains("Rated Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_ratedimpedance", ExtrVlist(list, i)); }
                    if (list[i].Contains("AES Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_aespower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Program Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_programpower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Peak Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_peakpower", ExtrVlist(list, i)); }
                    if (list[i].Contains("Sensitivity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_sensitivity", ExtrVlist(list, i)); }
                    if (list[i].Contains("Frequency Range")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_frequencyrange", ExtrVlist(list, i)); }
                    if (list[i].Contains("Power Compression @-10dB")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_powercompression10", ExtrVlist(list, i)); }
                    if (list[i].Contains("Power Compression @-3dB")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_powercompression3", ExtrVlist(list, i)); }
                    if (list[i].Contains("Power Compression @Full Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_powercompressionfull", ExtrVlist(list, i)); }
                    if (list[i].Contains("Max Recomm. Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_maxrecommfrequency", ExtrVlist(list, i)); }
                    if (list[i].Contains("Recomm. Enclosure Volume")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_recommenclosurevolume", ExtrVlist(list, i)); }
                    if (list[i].Contains("Minimum Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_minimumimpedance", ExtrVlist(list, i)); }
                    if (list[i].Contains("Max Peak To Peak Excursion")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_maxpeaktopeakexcursion", ExtrVlist(list, i)); }
                    if (list[i].Contains("Voice Coil Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_voicecoildiameter", ExtrVlist(list, i)); }
                    if (list[i].Contains("Voice Coil winding") || list[i].Contains("Voice Coil Winding")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_voicecoilwindingmaterial", ExtrVlist(list, i)); }
                    if (list[i].Contains("Suspension")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_suspension", ExtrVlist(list, i)); }
                    if (list[i].Contains("Cone")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "gen_cone", ExtrVlist(list, i)); }
                }

            }
        }
        if (edtFieldName == "hfspecifications")
        {
            for (int i = 0; i < list.Count; i++)
            {
                //standard hf specifications
                if (list[i].Contains("Throat Diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_throatdiameter", ExtrVlist(list, i)); }
                if (list[i].Contains("Rated Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_ratedimpedance", ExtrVlist(list, i)); }
                if (list[i].Contains("DC Resistance") || list[i].Contains("D.C. Resistance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_dcresistance", ExtrVlist(list, i)); }
                if (list[i].Contains("Minimum Impedance")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_minimumimpedance", ExtrVlist(list, i)); }
                if (list[i].Contains("Le (")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_le", ExtrVlist(list, i)); }
                if (list[i].Contains("Continuous Power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_continuouspower", ExtrVlist(list, i)); }
                if (list[i].Contains("Program power") || list[i].Contains("Max. program power")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_programpower", ExtrVlist(list, i)); }
                if (list[i].Contains("Sensitivity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_sensitivity", ExtrVlist(list, i)); }
                if (list[i].Contains("Frequency Range")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_frequencyrange", ExtrVlist(list, i)); }
                if (list[i].Contains("Min. Xover Frequency") || list[i].Contains("Min Xover Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_minxover", ExtrVlist(list, i)); }
                if (list[i].Contains("Recomm. Xover Frequency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_recomxover", ExtrVlist(list, i)); }
                if (list[i].Contains("Diaphragm material") || list[i].Contains("Diaphragm Material")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_diaphragmmaterial", ExtrVlist(list, i)); }
                if (list[i].Contains("Voice Coil Diameter") || list[i].Contains("Voice coil diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_voicecoildiameter", ExtrVlist(list, i)); }
                if (list[i].Contains("Voice Coil Winding") || list[i].Contains("Voice Coil winding") || list[i].Contains("Voice coil winding")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_voicecoilwindingmaterial", ExtrVlist(list, i)); }
                if (list[i].Contains("Magnet Material") || list[i].Contains("Magnet material")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_magnetmaterial", ExtrVlist(list, i)); }
                if (list[i].Contains("Flux Density")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_fluxdensity", ExtrVlist(list, i)); }
                if (list[i].Contains("Bl Factor")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_blfactor", ExtrVlist(list, i)); }
                if (list[i].Contains("Polarity")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "genhf_polarity", ExtrVlist(list, i)); }

            }
        }
        if (edtFieldName == "thielesmallparameters")
        {
            for (int i = 0; i < list.Count; i++)
            {
                //standard hf specifications
                if (list[i].Contains("Fs")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_fs", ExtrVlist(list, i)); }
                if (list[i].Contains("Re")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_re", ExtrVlist(list, i)); }
                if (list[i].Contains("Sd")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_sd", ExtrVlist(list, i)); }
                if (list[i].Contains("Qms")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_qms", ExtrVlist(list, i)); }
                if (list[i].Contains("Qes")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_qes", ExtrVlist(list, i)); }
                if (list[i].Contains("Qts")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_qts", ExtrVlist(list, i)); }
                if (list[i].Contains("Vas")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_vas", ExtrVlist(list, i)); }
                if (list[i].Contains("Mms")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_mms", ExtrVlist(list, i)); }
                if (list[i].Contains("Bl") || list[i].Contains("BL")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_bl", ExtrVlist(list, i)); }
                if (list[i].Contains("Mathematical Xmax")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_mathxmax", ExtrVlist(list, i)); }
                if (list[i].Contains("Le")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_le", ExtrVlist(list, i)); }
                if (list[i].Contains("Ref. Efficiency") || list[i].Contains("Ref. efficiency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_refeff", ExtrVlist(list, i)); }
                if (list[i].Contains("Half space efficiency")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "ths_hspaceff", ExtrVlist(list, i)); }

                

            }
        }
        if (edtFieldName == "mountinginformations")
        {
            for (int i = 0; i < list.Count; i++)
            {
                //standard hf specifications
                if (list[i].Contains("Overall diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_overall", ExtrVlist(list, i)); }
                if (list[i].Contains("holes and bolt")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_montholesbolt", ExtrVlist(list, i)); }
                if (list[i].Contains("Mounting holes diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_montholesdiameter", ExtrVlist(list, i)); }
                if (list[i].Contains("Bolt circle diameter")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_boltcirclediameter", ExtrVlist(list, i)); }
                if (list[i].Contains("Front mount baffle cutout")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_frontmountbaffle", ExtrVlist(list, i)); }
                if (list[i].Contains("Rear mount baffle cutout")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_rearmountbaffle", ExtrVlist(list, i)); }
                if (list[i].Contains("Total depth")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_totaldepth", ExtrVlist(list, i)); }
                if (list[i].Contains("Flange and gasket thickness")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_flangethick", ExtrVlist(list, i)); }
                if (list[i].Contains("Net weight")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_netweight", ExtrVlist(list, i)); }
                if (list[i].Contains("Shipping weight")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_shippingweight", ExtrVlist(list, i)); }
                if (list[i].Contains("Cardboard") || list[i].Contains("CardBoard")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_packagingdimensions", ExtrVlist(list, i)); }

                if (list[i].Contains("Mouth Height")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_mouthheight", ExtrVlist(list, i)); }
                if (list[i].Contains("Mouth Width")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_mouthwidth", ExtrVlist(list, i)); }
                if (list[i].Contains("Depth")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_depth", ExtrVlist(list, i)); }
                if (list[i].Contains("Mouth mounting dimension") || list[i].Contains("Mouth Mounting Dimension")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_mouthmountingdimension", ExtrVlist(list, i)); }
                if (list[i].Contains("Mouth mounting specs") || list[i].Contains("Mouth Mounting Specs")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_mouthmountingspecs", ExtrVlist(list, i)); }
                if (list[i].Contains("Driver mounting specs") || list[i].Contains("Driver Mounting Specs")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_drivermountingspecs", ExtrVlist(list, i)); }
                if (list[i].Contains("Flange Height")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_flangeheight", ExtrVlist(list, i)); }
                if (list[i].Contains("Flange Mounting")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_flangemounting", ExtrVlist(list, i)); }
                if (list[i].Contains("Gross Weight") || list[i].Contains("Gross weight")) { p.DataLangRecord.SetXmlProperty("genxml/textbox/" + "mou_grossweight", ExtrVlist(list, i)); }
                
            }
        }
            
            

        }

        public string ExtrVlist(List<string> l, int i)
        {
            string ret;
            ret = l[i + 1];
            if (ret.Trim() == "") ret = l[i + 2]; ;
            ret = ret.Replace("<br />", "");
            ret = ret.Replace("<br>", "");
            ret = ret.Replace("<\br>", "");
            ret = ret.Replace("\r\n", "");
            if (ret.Contains("<") && ret.Contains(">"))
            {
                var strremove = ret.Substring(ret.IndexOf("<"), ret.IndexOf(">") - ret.IndexOf("<") + 1);
                ret= ret.Replace(strremove, "");
            }
            ret = ret.Trim();
            return ret;
        }

        public string DecodeXmlString(string XMLData)
        {

            XMLData = XMLData.Replace("&amp;", "&");
            XMLData = XMLData.Replace("&lt;", "<");      // No code
            XMLData = XMLData.Replace("&gt;", ">");
            XMLData = XMLData.Replace("&amp;", "&");
            XMLData = XMLData.Replace("&lt;", "<");
            XMLData = XMLData.Replace("&gt;", ">");
            XMLData = XMLData.Replace("&divide;", "-");
            XMLData = XMLData.Replace("&plusmn;", "±");
            XMLData = XMLData.Replace("&oslash;", "Ø");
            XMLData = XMLData.Replace("&Oslash;", "Ø");
            XMLData = XMLData.Replace("&deg;", "°");
            XMLData = XMLData.Replace("&micro;", "µ");
            XMLData = XMLData.Replace("&trade;", "™");
            XMLData = XMLData.Replace("&bull;", "-");
            XMLData = XMLData.Replace("&reg;", "®");
            XMLData = XMLData.Replace("&nbsp;", "");
            XMLData = XMLData.Replace("&rdquo;", "\"");
            XMLData = XMLData.Replace("&quot;", @"""");

            return XMLData;
        }

        private int CreateCategory(XmlNode xmlNod, String titleAttrName, int parentCatId, Boolean disable)
        {
            var selectSingleNode = xmlNod.SelectSingleNode("./@" + titleAttrName);
            if (selectSingleNode != null)
            {
                var parentCatData = new CategoryData(parentCatId, EditLanguage);
                var parentCatRef = "";
                if (parentCatData.Exists) parentCatRef = parentCatData.CategoryRef;

                var catname = selectSingleNode.Value;
                var newcatref = parentCatRef + "-" + catname.Replace(" ", "").Replace("'", "") + parentCatId.ToString("");
                newcatref = newcatref.TrimStart('-');

                var categoryid = -1;

                // see if category already exists (update if so)
                var obj = ModCtrl.GetByGuidKey(PortalId, -1, "CATEGORY", newcatref);
                if (obj != null) categoryid = obj.ItemID;

                var categoryData = new CategoryData(categoryid, EditLanguage);

                // Build a breadcrumb catref, so we can try and identify this category on future imports.
                categoryData.DataRecord.SetXmlProperty("genxml/textbox/txtcategoryref", newcatref);
                categoryData.DataRecord.GUIDKey = categoryData.CategoryRef;

                categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtcategoryname", catname);
                categoryData.ParentItemId = parentCatId;

                categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseoname", catname);
                categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtseopagetitle", catname);

                selectSingleNode = xmlNod.SelectSingleNode("./@MetaDesc");
                if (selectSingleNode != null)
                {
                    categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtcategorydesc", selectSingleNode.InnerText);
                    categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtmetadescription", selectSingleNode.InnerText);
                }

                var tagwords = "";
                for (int i = 1; i < 11; i++)
                {
                    var motCle = "MotCle" + i.ToString("");
                    selectSingleNode = xmlNod.SelectSingleNode("./@" + motCle);
                    if (selectSingleNode != null && selectSingleNode.InnerText != "") tagwords = "," + selectSingleNode.InnerText;
                }
                tagwords = tagwords.TrimStart(',');
                categoryData.DataLangRecord.SetXmlProperty("genxml/textbox/txtmetakeywords", tagwords);

                if (disable)
                {
                    categoryData.DataRecord.SetXmlProperty("genxml/checkbox/chkdisable", "True");
                }


                //-------------------------------------------------------------------------
                // for development ONLY, so we can cleardown easy
                // Remove for production
                //categoryData.DataRecord.SetXmlProperty("genxml/createdby", "quickcategory");
                //-------------------------------------------------------------------------

                categoryData.IsHidden = false;

                categoryData.Save();
                return categoryData.CategoryId;
            }
            return -1;
        }
    }


}
