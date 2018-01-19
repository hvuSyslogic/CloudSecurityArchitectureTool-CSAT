package gov.nist.csrc.spreadsheet;

import gov.nist.csrc.jooq.tables.records.*;
import gov.nist.csrc.ui.MainWindow;
import org.apache.log4j.Logger;
import org.apache.poi.openxml4j.exceptions.InvalidFormatException;
import org.apache.poi.openxml4j.opc.OPCPackage;
import org.apache.poi.ss.usermodel.Row;
import org.apache.poi.xssf.usermodel.XSSFRow;
import org.apache.poi.xssf.usermodel.XSSFSheet;
import org.apache.poi.xssf.usermodel.XSSFWorkbook;
import org.jooq.DSLContext;
import org.jooq.Record1;
import org.jooq.Result;

import java.io.FileInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.regex.Pattern;

/**
 * Created by naw2 on 12/22/2017.
 */
public class UpdateDB {
    private static final Logger log = Logger.getLogger(MainWindow.class.getName());

    private final DSLContext context;

    private final gov.nist.csrc.jooq.tables.Specs SPECS;
    private final gov.nist.csrc.jooq.tables.Controls CONTROLS;
    private final gov.nist.csrc.jooq.tables.Baselinesecuritymappings BASELINESECURITYMAPPINGS;
    private final gov.nist.csrc.jooq.tables.Capabilities CAPABILITIES;

    private boolean implementation3Col = false;

    public UpdateDB(DSLContext context) {
        this.context = context;

        SPECS = gov.nist.csrc.jooq.tables.Specs.SPECS;
        CONTROLS = gov.nist.csrc.jooq.tables.Controls.CONTROLS;
        BASELINESECURITYMAPPINGS = gov.nist.csrc.jooq.tables.Baselinesecuritymappings.BASELINESECURITYMAPPINGS;
        CAPABILITIES  = gov.nist.csrc.jooq.tables.Capabilities.CAPABILITIES;
    }

    public void setImplementation3Col(boolean implementation3Col) {
        this.implementation3Col = implementation3Col;
    }

    /**
     * Opens Excel sheet for reading
     * @param path Path to excel sheet
     * @param position Workbook to return
     */
    private static XSSFSheet openSheet(String path, int position) {
        log.info("Opening new excel workbook from path " + path + " (opening workbook pos " + position + ")");
        XSSFWorkbook workbook;
        try {
            InputStream inputStream = new FileInputStream(path);
            OPCPackage pkg = OPCPackage.open(inputStream);
            workbook = new XSSFWorkbook(pkg);
            pkg.close();
        } catch (InvalidFormatException | IOException e) {
            log.error("Error opening excel sheet, returning null", e);
            return null;
        }
        return workbook.getSheetAt(position);
    }

    /**
     * Checks baseline security mappings, capabilities, and controls. Returns false if any are empty
     * @return If the database is usable in its current state
     */
    public boolean isComplete() {
        log.info("Checking if records exist for baselines, capabilities and controls");
        Result<Record1<Integer>> baselinesResult = context.select(BASELINESECURITYMAPPINGS.ID)
                .from(BASELINESECURITYMAPPINGS).fetch();
        Result<Record1<Integer>> capabilitiesResult = context.select(CAPABILITIES.ID)
                .from(CAPABILITIES).fetch();
        Result<Record1<Integer>> controlsResult = context.select(CONTROLS.ID)
                .from(CONTROLS).fetch();
        return !(baselinesResult.isEmpty() || capabilitiesResult.isEmpty() || controlsResult.isEmpty());
    }

    /**
     * Update Capabilities, TIC Mappings and MapTypeCapabilitiesControls
     * @param path to workbook
     */
    public void updateCapabilities(String path) {
        log.info("Updating capabilities");
        XSSFSheet sheet = openSheet(path, 2);

        if(sheet == null)
            return;

        // process tic capabilities and tic mappings
        log.info("Processing capabilities");
        List<CapabilitiesRecord> capabilities = new ArrayList<>();
        HashMap<String, TicmappingsRecord> ticmappings = new HashMap<>();
        for(int i = 3; i < sheet.getPhysicalNumberOfRows(); i++) {
            XSSFRow row = sheet.getRow(i);

            if(row == null || row.getPhysicalNumberOfCells() < 42) {
                break;
            }

            CapabilitiesRecord capability = new CapabilitiesRecord();
            capability.setDomain(row.getCell(0).getStringCellValue());
            capability.setContainer(row.getCell(1).getStringCellValue());
            capability.setCapability(row.getCell(2).getStringCellValue());
            capability.setCapability2(row.getCell(3).getStringCellValue());
            capability.setDescription(row.getCell(4).getStringCellValue());
            // NOTE CSA Description missing
            String uniqueId = row.getCell(5).getStringCellValue();
            capability.setUniqueid(uniqueId);
            capability.setScopes(row.getCell(6).getStringCellValue());
            //log.debug(row.getCell(23).getStringCellValue());
            capability.setC((int) row.getCell(26).getNumericCellValue());
            capability.setI((int) row.getCell(27).getNumericCellValue());
            capability.setA((int) row.getCell(28).getNumericCellValue());

            capability.setResponsibilityvector(row.getCell(31).getStringCellValue() + "," +
                    row.getCell(35).getStringCellValue() + "," +
                    row.getCell(32).getStringCellValue() + "," +
                    row.getCell(36).getStringCellValue() + "," +
                    row.getCell(33).getStringCellValue() + "," +
                    row.getCell(37).getStringCellValue());
            capability.setOtheractors(row.getCell(39).getStringCellValue() + "," +
                    row.getCell(40).getStringCellValue() + "," +
                    row.getCell(41).getStringCellValue() + "," +
                    row.getCell(43).getStringCellValue() + "," +
                    row.getCell(45).getStringCellValue());

            String ticMappingsString = row.getCell(7).getStringCellValue();
            String[] entries = ticMappingsString.split("[;\n,]");
            for(String ticCap:entries) {
                TicmappingsRecord ticData = new TicmappingsRecord();
                ticData.setTicname(ticCap);
                ticmappings.put(uniqueId, ticData);
            }
            if(capability.getUniqueid().equals("Content Filtering2")) {
                log.debug("Contains ContentFiltering2");
            }
            capabilities.add(capability);
        }
        context.batchStore(capabilities).execute();
        // find correct capabilitiesId for each ticMapping
        log.info("Processing TIC Mappings");
        List<TicmappingsRecord> ticList = new ArrayList<>();
        log.info("Ticmapping keys: " + ticmappings.keySet());
        for(String uid:ticmappings.keySet()) {
            Integer possibleId = context.select(CAPABILITIES.ID).from(CAPABILITIES).where(CAPABILITIES.UNIQUEID.eq(uid))
                    .fetch().get(0).value1();
            TicmappingsRecord ticMapping = ticmappings.get(uid);
            ticMapping.setCapabilityid(possibleId);
            ticList.add(ticmappings.get(uid));
        }
        context.batchStore(ticList).execute();

        // process MapTypes
        log.info("Processing map types");
        List<MaptypescapabilitiescontrolsRecord> mapList = new ArrayList<>();
        for(int i = 3; i < sheet.getPhysicalNumberOfRows(); i++) {
            XSSFRow row = sheet.getRow(i);

            if(row == null || row.getPhysicalNumberOfCells() < 42) {
                break;
            }

            int capId = context.select(CAPABILITIES.ID).from(CAPABILITIES)
                    .where(CAPABILITIES.UNIQUEID.eq(row.getCell(5).getStringCellValue()))
                    .fetch().get(0).value1();
            for(int level = 1; level <= 7; level++) {
                String implementList;
                if(implementation3Col) {
                    implementList = row.getCell( 8 + level - 1).getStringCellValue();
                } else {
                    if(level <= 3) {
                        implementList = row.getCell(8 + (4 * (level - 1)), Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue() + ","
                                + row.getCell(8 * + (4 * (level - 1) + 1), Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue();
                    } else {
                        implementList = row.getCell(20 + level - 4).getStringCellValue();
                    }
                }

                List<String> controls = getControlList(implementList);
                for(String controlName:controls) {
                    String topControl = removeSpec(controlName);

                    List<ControlsRecord> possibleControls = context.selectFrom(CONTROLS)
                            .where(CONTROLS.NAME.eq(topControl)).fetch();
                    int controlId = (possibleControls.isEmpty()) ? 0 : possibleControls.get(0).getId();

                    Result<Record1<Integer>> result = context.select(SPECS.ID)
                            .from(SPECS)
                            .where(SPECS.CONTROLSID.eq(controlId)
                                    .and(SPECS.SPECIFICATIONNAME.eq(getTopControlName(controlName))))
                            .fetch();
                    int specId = (result.isEmpty()) ? 0 : result.get(0).value1();

                    if(controlId > 0 || specId > 0) {
                        boolean isControl;
                        if(specId == 0) {
                            isControl = true;
                            specId = 1;
                        } else {
                            isControl = false;
                        }
                        MaptypescapabilitiescontrolsRecord map = new MaptypescapabilitiescontrolsRecord();
                        map.setCapabilitiesid(capId);
                        map.setControlsid(controlId);
                        map.setMaptypesid(level);
                        map.setSpecsid(specId);
                        map.setIscontrolmap(isControl);
                        mapList.add(map);
                    }
                }
            }
        }
        context.batchStore(mapList).execute();
    }

    private static boolean isRow4Control(String cell) {
        return cell.replaceAll("[A-Z]{2}-([0-9]{1,2})", "").length() == 0;
    }

//    private int findPriorityId(XSSFRow row) {
//
//    }
//
//    private int findFamilyId(XSSFRow row) {
//
//    }
//
//    private int findBaselineId(XSSFRow row) {
//        String baselineDescription = (row.getCell())
//    }

    private boolean controlExistsInDb(ControlsRecord record) {
        return context.select(CONTROLS.ID).from(CONTROLS).where(CONTROLS.NAME.eq(record.getName()))
                .and(CONTROLS.DESCRIPTION.eq(record.getDescription()))
                .and(CONTROLS.GUIDANCE.eq(record.getGuidance())).fetch().size() == 0;
    }

    /**
     * Updates Controls, Specs, Relateds
     * @param path Path to workbook
     */
    public void updateControls(String path) {
        log.info("Updating controls");
        XSSFSheet sheet = openSheet(path, 12);

        if(sheet == null)
            return;

        List<ControlsRecord> controlsList = new ArrayList<>();
        HashMap<String, ControlsRecord> controlNames = new HashMap<>();
        HashMap<String, List<String>> controlRelated = new HashMap<>();
        HashMap<String, HashMap<String, SpecsRecord>> controlSpecs = new HashMap<>();

        for(int i = 11; i < sheet.getPhysicalNumberOfRows(); i++) {
            XSSFRow row = sheet.getRow(i);
            if(row == null || row.getPhysicalNumberOfCells() < 6) {
                break;
            }
            int ROW_DESCRIPTION = 1;
            String description = row.getCell(ROW_DESCRIPTION, Row.MissingCellPolicy.CREATE_NULL_AS_BLANK)
                    .getStringCellValue();
            if(description.contains("Withdrawn")) { // TODO better checking for withdrawn cells
                continue;   // skip "withdrawn" cells
            }

            String specsName;
            String specsTail;
            String specsPrefix;

            String cellData = row.getCell(0, Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue();

            if(!isRow4Control(cellData)) {
                //specs row
                specsName = cellData.replace(" ", "");
                specsTail = getTopControlName(cellData);
                specsPrefix = specsName.replace(specsTail, "");
                // Get base control for the current specs
                if(controlNames.containsKey(specsPrefix)) {
                    if(!controlSpecs.containsKey(specsPrefix)) {
                        controlSpecs.put(specsPrefix, new HashMap<>());
                    }
                    SpecsRecord newSpecs = new SpecsRecord();
                    newSpecs.setSpecificationname(specsTail);
//                    newSpecs.setDescription();
//                    newSpecs.setGuidance();
                    controlSpecs.get(specsPrefix).put(specsTail, newSpecs);
//                } else if(!(specsTail.isEmpty())) {
                } else {
                    log.fatal("No specs for " + specsName);
                    return;
                }
            } else {
                // controls row
                ControlsRecord control = new ControlsRecord();
//                control.setBaselineid();
//                control.setFamilyid();
//                control.setPriorityid();
//
                control.setName(cellData.replace(" ", ""));
                control.setDescription(row.getCell(1, Row.MissingCellPolicy.CREATE_NULL_AS_BLANK)
                        .getStringCellValue());
//                control.setGuidance();

                if(!controlExistsInDb(control)) {
                    controlsList.add(control);
                } else {
                    log.warn("Control" + control.getName() + " exists in database already, skipping...");
                    continue;
                }
                if(!controlNames.containsKey(control.getName())) {
                    controlNames.put(control.getName(), control);
                } else {
                    log.fatal("Control " + control.getName() + " exists more then once in controls spreadsheet");
                    return;
                }
            }


        }
        context.batchStore(controlsList).execute();

        // persist all of the related specs for controls

        List<SpecsRecord> controlSpecFinal = new ArrayList<>();
        // loop over controlSpecs
        context.batchStore(controlSpecFinal).execute();

        List<RelatedsRecord> relateds = new ArrayList<>();
        // loop over control relateds
        context.batchStore(relateds).execute();
    }

    /**
     * Update BaselineSecurityMappings table
     *
     * Requires controls and specs to be up to date
     * @param path to workbook
     */
    public void updateBaselineSecurityMappings(String path) {
        log.info("Updating baseline security mappings");
        XSSFSheet sheet = openSheet(path, 0);

        if(sheet == null)
            return;

        List<BaselinesecuritymappingsRecord> baselines = new ArrayList<>();
        for(int i = 2; i < sheet.getPhysicalNumberOfRows(); i++) {
            log.debug("Processing line " + i + " of BaselineSecurityMappings spreadsheet");
            XSSFRow row = sheet.getRow(i);
            if(row == null || row.getPhysicalNumberOfCells() < 6) {
                break;
            }
            int AUTHOR_NIST = 1;
            int AUTHOR_FEDRAMP = 2;
            int LEVEL_LOW = 1;
            int LEVEL_MED = 2;
            int LEVEL_HIGH = 3;
            int COL_NIST_LOW = 2;
            int COL_FED_LOW = 3;
            int COL_NIST_MED = 5;
            int COL_FED_MED = 6;
            int COL_NIST_HIGH = 7;
            int COL_FED_HIGH = 8;
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_NIST_LOW,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_LOW, AUTHOR_NIST));
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_FED_LOW,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_LOW, AUTHOR_FEDRAMP));
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_NIST_MED,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_MED, AUTHOR_NIST));
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_FED_MED,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_MED, AUTHOR_FEDRAMP));
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_NIST_HIGH,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_HIGH, AUTHOR_NIST));
            baselines.addAll(createBaselineSecurityMappings(row.getCell(COL_FED_HIGH,
                    Row.MissingCellPolicy.CREATE_NULL_AS_BLANK).getStringCellValue(), LEVEL_HIGH, AUTHOR_FEDRAMP));

        }
        log.info("Storing " + baselines.size() + " new BaselineSecurityMappings");
        context.batchStore(baselines).execute();
    }

    /**
     * Insert new record into BaselineSecurityMappings with string containing many controls
     * @param component String containing many controls (parsed out with regex)
     * @param level (low, medium, or high corresponding to 1, 2 or 3)
     * @param author (1 denotes NIST, 2 denotes FEDRAMP)
     */
    private List<BaselinesecuritymappingsRecord> createBaselineSecurityMappings(String component, int level,
                                                                                int author) {
        List<BaselinesecuritymappingsRecord> records = new ArrayList<>();
        List<String> controls = getControlList(component);
        for(String entry:controls) {
            boolean isControlMap = Pattern.matches("[A-Z]{2}-([0-9]{1,2})", entry);
            int specsId = 1;
            int controlsId = 1;
            if(isControlMap) {
                List<ControlsRecord> filteredControls = context.selectFrom(CONTROLS).where(CONTROLS.NAME.eq(entry))
                        .fetch();
                if(filteredControls.size() >= 1) {
                    controlsId = filteredControls.get(0).getId();
                } else {
                    log.error("Could not find list of controls for given controls: " + entry);
                }
            } else {
                String top = removeSpec(entry);
                // get controls id
                Result<Record1<Integer>> result = context.select(CONTROLS.ID).from(CONTROLS)
                        .where(CONTROLS.NAME.eq(top)).fetch();
                int specCotrolId = result.isEmpty() ? 0 : result.get(0).value1();
                if(specCotrolId == 0) {
                    log.error("Could not find list of controls for given controls: " + entry);
                }
                // get specs id
                result = context.select(SPECS.ID).from(SPECS)
                        .where(SPECS.CONTROLSID.eq(specCotrolId)
                                .and(SPECS.SPECIFICATIONNAME.eq(entry.replace(top, "")))).fetch();
                specsId = result.isEmpty() ? 0 : result.get(0).value1();
            }

            BaselinesecuritymappingsRecord baseline = context.newRecord(BASELINESECURITYMAPPINGS);
            baseline.setLevel(level);
            baseline.setBaselineauthor(author);
            baseline.setIscontrolmap(isControlMap);
            baseline.setSpecsid(specsId);
            baseline.setControlsid(controlsId);
            records.add(baseline);
        }
        return records;
    }

    private static List<String> getControlList(String rawString) {
        rawString = rawString.replace(" ", "");
        //noinspection RegExpRedundantEscape
        String[] rawControls = rawString.split("([,;\\n\\t *\\[\\]\\{\\}])");

        List<String> controls = new ArrayList<>();
        for(String potentialControl:rawControls) {
            if(Pattern.matches("[A-Z]{2}-([0-9]{1,2})(\\((\\d|\\d\\d)\\)|)?", potentialControl)) {
                controls.add(potentialControl);
            } else {
                log.warn("Malformed control: Pattern mismatch for " + potentialControl);
            }
        }
        return controls;
    }

    private static String getTopControlName(String rawString) {
        rawString = rawString.replace(" ", "");
        rawString = rawString.replaceAll("[A-Z]{2}-([0-9]{1,2})", "");

        return rawString;
    }

    private static String removeSpec(String control) {
        String tail = getTopControlName(control);
        return (control.equals("")) ? control : control.substring(0, control.indexOf(tail));
    }
}
