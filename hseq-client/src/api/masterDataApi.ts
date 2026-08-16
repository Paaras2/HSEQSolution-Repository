import { apiGet } from '../lib/httpClient'
import type {
  DocumentTypeLookup,
  OrganizationalActivityLookup,
  OrganizationalManagementLookup,
  ProjectLookup,
} from '../types/api'

export const masterDataApi = {
  async getProjects(): Promise<ProjectLookup[]> {
    return apiGet<ProjectLookup[]>('/MasterData/projects')
  },
  async getManagements(): Promise<OrganizationalManagementLookup[]> {
    return apiGet<OrganizationalManagementLookup[]>('/MasterData/organizational-managements')
  },
  async getActivities(): Promise<OrganizationalActivityLookup[]> {
    return apiGet<OrganizationalActivityLookup[]>('/MasterData/organizational-activities')
  },
  async getDocumentTypes(): Promise<DocumentTypeLookup[]> {
    return apiGet<DocumentTypeLookup[]>('/MasterData/document-types')
  },
}
