export interface DoctorInformation {
  doctorNameEnglish: string
  doctorNamePashto: string
  doctorProfessionPashto: string
  doctorProfessionEnglish: string
  doctorPhoto: string | null
  doctorLogo: string | null
  hospitalLogo: string | null
  hospitalNamePashto: string
  hospitalAddress: string
  hospitalPhoneNumbers: string
  loggedInUserId: number
  exists: boolean
}

export type DoctorImageField = 'doctorPhoto' | 'doctorLogo' | 'hospitalLogo'
