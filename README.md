# Introduction 
 - The inventory syncronizer follows the following steps to update stock.
	1. It pulls all woocommerce products and uses their skus to match against SAP products. SAP is the master and controlls the stock levels. The syncronizer updates one way meaning it only updates woocomerce stock levels  based on what is in the warehouses.
	 - the following are the rules used to match products. 
		- @SE for short Exp on woocommerce  on sap they are located in warehouse 04
		- @D for Damaged on sap they are located in warehouse 02
		- @F for flash
		- $X for bulk packs where the X is the size of�the�bulk�packs on sap they are located in warehouse 01 $06 and are defined by property U_BlQty 
		- Normal products have their skus without any special postfix on sap they are located in warehouse 01 $06
		- 
	2. The syncronizer updates normal products, and sets the stock to match levels on SAP and also updates bulk products based on the balpack quantity.
	
	3. Then Short expiry products are updated based on the amount of quantity in the short expiry warehouse. If a product on woocommerce is published but stock leve is 0 on SAP then the item is set to draft on woocomerce.
	
	4. Damanged products are updated based on the amount of quantity in the damaged goods warehouse. If a product on woocommerce is published but stock leve is 0 on SAP then the item is set to draft on woocommerce.


#Bulk Pack Items Update
To update the Bulk pack items on woocommerce we follow the following steps.

1) Pull all woocomerce products
2) Pull all products in warehouse 01 and 06 and do a summation of the two warehouses to get the current stock quantity/products in hand.
3) To identify SAP products that are bulk pack, they have a UDF named "U_BlPack" which will be "Y" if the product is a bulk pack item and another field will be "U_BlQty" which dermines the bulk pack quantity.
4) To get the product sku, append dollar sign ($) followed by the balk pack qunatity as a postfix to the the item code/sku. For example if an item has the code PSK123 on sap and is marked a bulk pack item with a quantity of 24, the resulting sku should be PSK123$24.
5) To match a woocomerce product comapre the above sku with the sku of the woocomerce products. If a matching product is found the process continues with the update otherwise this product will be included in the logs and the notification emails.
6) Once a woocomerce products is identified and matched to an SAP product we then perform the stock calcultaion where the total stock is devided by the bulk pack quatity to get the balk pack stock quantity.
7) If the SAP stock quantity is less than the balk pack quantity or is 0, the stock level on woocomerce is set to 0.
8) If the stock quantity does not perfectly devide into bulk packs, the maximum number of bulk packs is set as the stock level. For example if the stock in sap is 50 and the bulk pack quantity is 24, the stock is set to 2 since we have two comple bulk packs of 24 and the extra 2 items are ignored as they do not form a complate bukl pack.